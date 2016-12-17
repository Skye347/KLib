using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using KLib.NetCore.Callback;
using KLib.NetCore.Protocol;
using static KLib.Log.Log;
using System.Threading;
using System.Collections.Concurrent;

namespace KLib.NetCore
{
    public class SimpleCore : Core
    {
        override public void SetClient(CallbackCollection callbackCollection, bool SingleConnect)
        {
            _Callback = callbackCollection;
            _Callback.BindCore(this);
            _SocketCollection = new List<SocketStateObject>();
            _ConnectingSocketCollection = new List<Socket>();
            _isServer = false;
            _isSet = true;
            _SingleConnect = SingleConnect;
        }
        override public void SetServer(String ip, int port, CallbackCollection callbackCollection, int MAX_LISTEN = 10)
        {
            _IpAddress = IPAddress.Parse(ip);
            _Port = port;
            _Callback = callbackCollection;
            _SocketCollection = new List<SocketStateObject>();
            acceptThreadLock = new object();
            _isSet = true;
            _isServer = true;
            _MAX_LISTEN = MAX_LISTEN;
        }
        protected bool _isServer, _isSet = false, _isOnline = false;
        protected Socket _Socket;
        private List<Socket> _ConnectingSocketCollection;
        private List<SocketStateObject> _SocketCollection;
        private object acceptThreadLock;
        override public Boolean StartListen()
        {
            if (!_isSet)
            {
                return false;
            }
            if (!_isServer)
            {
                return false;
            }
            _Socket = _Callback._ProtocolOp.StartListen(_IpAddress, _Port);
            _isOnline = true;
            return true;
        }
        override public Boolean Connect(String ip, int port, bool GoAsync = false)
        {
            if (!_isSet)
            {
                return false;
            }
            if (_isServer)
            {
                return false;
            }
            IPAddress ipAddress = IPAddress.Parse(ip);
            SocketException err;
            Socket tmpSocket = _Callback._ProtocolOp.Connect(ipAddress, port, out err);
            if (err != null)
            {
                return false;
            }
            if (_SingleConnect)
            {
                if (_Socket == null)
                {
                    _Socket = tmpSocket;
                    _isOnline = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            _ConnectingSocketCollection.Add(tmpSocket);
            _isOnline = true;
            return true;
        }
        private bool ConnectAsync(String ip, int port)
        {
            if (!_isSet)
            {
                return false;
            }
            if (_isServer)
            {
                return false;
            }
            IPAddress ipAddress = IPAddress.Parse(ip);
            Socket tmpSocket = _Callback._ProtocolOp.ConnectAsync(ipAddress, port);
            if (_SingleConnect)
            {
                if (_Socket == null)
                {
                    _Socket = tmpSocket;
                    _isOnline = true;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            _ConnectingSocketCollection.Add(tmpSocket);
            _isOnline = true;
            return true;
        }
        override public void Stop()
        {
            _isOnline = false;
        }
        public void AcceptThread()
        {
            while (_isOnline)
            {
                Socket clientSocket = _Callback._ProtocolOp.GetClient(_Socket);
                lock (acceptThreadLock)
                {
                    _SocketCollection.Add(new SocketStateObject(clientSocket));
                }
            }
        }
        override public void Run()
        {
            SocketException err = null;
            if (_isServer)
            {
                Thread acceptThread = new Thread(AcceptThread);
                acceptThread.Start();
                while (_isOnline)
                {
                    lock (acceptThreadLock)
                    {
                        for (int i = _SocketCollection.Count - 1; i >= 0; i--)
                        {
                            var connect = _SocketCollection[i];
                            if (connect.socket.Poll(0, SelectMode.SelectRead))
                            {
                                byte[] data = _Callback._ProtocolOp.Receive(connect.socket, out err);
                                if (err != null)
                                {
                                    log("Connection Close", 1, "Core.Run");
                                    _SocketCollection.RemoveAt(i);
                                    connect.socket.Dispose();
                                    continue;
                                }
                                if (data.Length == 0 && connect.socket.Available == 0)
                                {
                                    log("Connection Close", 1, "Core.Run");
                                    _SocketCollection.RemoveAt(i);
                                    connect.socket.Dispose();
                                    continue;
                                }
                                connect.state = _Callback.Received(data, connect.socket, out err, connect.state);
                                if(err!=null)
                                {
                                    log(err.Message, 3, "Core.Run");
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (_SingleConnect)
                {
                    object SocketState=null;
                    _Callback.Connected(_Socket, out err);
                    if (err != null)
                    {
                        log("Connection Close", 1, "Core.Run");
                        _Socket.Dispose();
                        return;
                    }
                    while (_isOnline)
                    {
                        var connect = _Socket;
                        if (connect.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] data = _Callback._ProtocolOp.Receive(connect, out err);
                            if (err != null)
                            {
                                log("Connection Close", 1, "Core.Run");
                                connect.Dispose();
                                _isOnline = false;
                                break;
                            }
                            if (data.Length == 0 && connect.Available == 0)
                            {
                                log("Connection Close", 1, "Core.Run");
                                connect.Dispose();
                                _isOnline = false;
                                break;
                            }
                            SocketState = _Callback.Received(data, connect, out err, connect);
                            if (err != null)
                            {
                                log(err.Message, 3, "Core.Run");
                            }
                        }
                    }
                    return;
                }
                while (_isOnline)
                {
                    if (_ConnectingSocketCollection.Count == 0 && _SocketCollection.Count == 0)
                    {
                        _isOnline = false;
                        break;
                    }
                    for (int i = _ConnectingSocketCollection.Count - 1; i >= 0; i--)
                    {
                        var connect = _ConnectingSocketCollection[i];
                        if (connect.Connected)
                        {
                            _Callback.Connected(connect, out err);
                            _ConnectingSocketCollection.RemoveAt(i);
                            if (err != null)
                            {
                                connect.Dispose();
                                continue;
                            }
                            _SocketCollection.Add(new SocketStateObject(connect));
                        }
                    }
                    for (int i = _SocketCollection.Count - 1; i >= 0; i--)
                    {
                        var connect = _SocketCollection[i];
                        if (connect.socket.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] data = _Callback._ProtocolOp.Receive(connect.socket, out err);
                            if (err != null)
                            {
                                log("Connection Close", 1, "Core.Run");
                                _SocketCollection.RemoveAt(i);
                                connect.socket.Dispose();
                                continue;
                            }
                            if (data.Length == 0 && connect.socket.Available == 0)
                            {
                                log("Connection Close", 1, "Core.Run");
                                _SocketCollection.RemoveAt(i);
                                connect.socket.Dispose();
                                continue;
                            }
                            connect.state = _Callback.Received(data, connect.socket, out err, connect.state);
                            if (err != null)
                            {
                                log(err.Message, 3, "Core.Run");
                            }
                        }
                    }
                }
            }
        }

        private void E_Completed(object sender, SocketAsyncEventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}