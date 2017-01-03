using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using KLib.NetCore.Callback;
using KLib.MemHper;
using KLib.NetCore.Protocol;
using static KLib.Log.Log;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace KLib.NetCore
{
    public class UniAsynCore : Core
    {
        //private SocketAsyncPool _ConnectPool;
        private UniNetObject _ServerUniObject;
        private CoreType _Type;
        private KLib.MemHper.Buffer _BufferManager;
        public override bool Connect(string ip, int port, bool GoAsync = true)
        {
            if (_SingleConnect && _ServerUniObject != null)
            {
                return false;
            }
            UniNetObject e = new UniNetObject();
            e.SetProtocol(_Callback._ProtocolOp);
            //SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            //byte[] buf = new byte[4096];
            //e.SetBuffer(buf, 0, 4096);
            e.SetCompletedHandler(ProcessIO);
            //e.Completed += new EventHandler<SocketAsyncEventArgs>(ProcessIO);
            e.SetRemoteEndPoint(new IPEndPoint(IPAddress.Parse(ip), port));
            e.LastOperation = UniNetOperation.Connect;
            e.ConnectAsync();
            //e.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            //Socket tempServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            //e.UserToken = new SocketStateObject(tempServerSocket);
            ////_ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            //if (!tempServerSocket.ConnectAsync(e))
            //{
            //    ProcessConnected(e);
            //}
            return true;
        }

        public override void Run()
        {
            if (_Type == CoreType.Server)
            {
                RunServer();
            }
            else
            {
                RunClient();
            }
        }

        private void RunServer()
        {
            StartAccept(null);
        }

        private void RunClient()
        {
            //ProcessReceive(_ServerArgs);
        }

        private void StartAccept(UniNetObject uniObject)
        {
            if (uniObject == null)
            {
                uniObject = new UniNetObject();
                uniObject.SetProtocol(_Callback._ProtocolOp);
                uniObject.SetCompletedHandler(GetAccepted);
                uniObject.LastOperation = UniNetOperation.Accept;
                //Args.Completed += new EventHandler<SocketAsyncEventArgs>((object sender,SocketAsyncEventArgs AcceptArgs)=> {
                //    GetAccepted(AcceptArgs);
                //});
            }
            //else
            //{
            //    Args.AcceptSocket = null;
            //}
            if (!_ServerUniObject.AcceptAsync(uniObject))
            {
                GetAccepted(uniObject);
            }
        }

        private void GetAccepted(UniNetObject AcceptedUniObject)
        {
            if (AcceptedUniObject.ObjectError != Error.NetCoreError.Success)
            {
                StartAccept(AcceptedUniObject);
                return;
            }
            NetCore.Error.NetCoreException err;
            UniNetObject clientUniObject = new UniNetObject();
            clientUniObject.SetProtocol(_Callback._ProtocolOp);
            _BufferManager.SetBuffer(clientUniObject.SetBuffer, 4096);
            clientUniObject.SetCompletedHandler(ProcessIO);
            clientUniObject = _Callback._ProtocolOp.GetAcceptedUniObject(AcceptedUniObject, ref clientUniObject);
            _Callback.Accepted(clientUniObject, out err);
            if (clientUniObject.BufferLength != 0)
            {
                _Callback.Received(clientUniObject.Buffer, clientUniObject, out err, clientUniObject.stateObject);
            }
            clientUniObject.BufferLength = 0;
            if (err != null)
            {
                //ProcessBadConnection(uniObject);
                StartAccept(AcceptedUniObject);
                return;
            }
            //var clientArgs=_ConnectPool.Pop();
            //clientArgs.UserToken=new SocketStateObject(clientSocket);
            clientUniObject.LastOperation = UniNetOperation.Receive;
            if (!clientUniObject.ReceiveAsync(clientUniObject))
            {
                ProcessReceive(clientUniObject);
            }
            StartAccept(AcceptedUniObject);
        }

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private void ProcessConnected(UniNetObject uniObject)
        {
            NetCore.Error.NetCoreException err;
            var state=_Callback.Connected(uniObject, out err);
            if (err != null)
            {
                ProcessBadConnection(uniObject);
                return;
            }
            uniObject.stateObject = state;
            //connectionArgs.UserToken = new SocketStateObject(connectionArgs.ConnectSocket,state);
            _BufferManager.SetBuffer(uniObject.SetBuffer, 4096);
            uniObject.LastOperation = UniNetOperation.Receive;
            if (!uniObject.ReceiveAsync(uniObject))
            {
                ProcessReceive(uniObject);
            }
        }

        private void ProcessBadConnection(UniNetObject uniObject)
        {
            log("bad connection", ERROR, "ProcessBadConnection");
            _Callback.Aborted(uniObject, uniObject.stateObject);
            //if (_ConnectPool == null)
            //{
                uniObject.Dispose();
            //}
            //else
            //{
            //    //_ConnectPool.Push(connectionArgs);
            //}
            return;
        }

        private void ProcessReceive(UniNetObject uniObject)
        {   //bad connection
            if (uniObject.ObjectError != Error.NetCoreError.Success)
            {
                ProcessBadConnection(uniObject);
                return;
            }
            //if (connectionArgs.BytesTransferred == 0)
            //{
            //    (connectionArgs.UserToken as SocketStateObject).socket.Shutdown(SocketShutdown.Both);
            //    return;
            //}
            //bad connection
            //receive
            //byte[] buffer = new byte[connectionArgs.BytesTransferred];
            //Socket clientSocket = (connectionArgs.UserToken as SocketStateObject).socket;
            //Array.Copy(connectionArgs.Buffer, buffer, connectionArgs.BytesTransferred);
            byte[] buffer = uniObject.ReceiveAll();
            if (buffer == null)
            {
                ProcessBadConnection(uniObject);
                return;
            }
            //receive
            NetCore.Error.NetCoreException err;
            var State = _Callback.Received(buffer, uniObject, out err, uniObject.stateObject);
            if (err != null)
            {
                ProcessBadConnection(uniObject);
                return;
            }
            uniObject.stateObject=State;
        }

        private void ProcessIO(UniNetObject uniObject)
        {
            if (uniObject.ObjectError != Error.NetCoreError.Success)
            {
                ProcessBadConnection(uniObject);
                return;
            }
            if (uniObject.LastOperation == UniNetOperation.Accept)
            {
                StartAccept(uniObject);
            }
            else if (uniObject.LastOperation == UniNetOperation.Receive)
            {
                ProcessReceive(uniObject);
                if (uniObject.innerObject==null)
                {
                    return;
                    //ProcessBadConnection(connectionArgs)
                }
                if (!uniObject.ReceiveAsync(uniObject))
                {
                    ProcessReceive(uniObject);
                }
            }
            else if (uniObject.LastOperation == UniNetOperation.Connect)
            {
                ProcessConnected(uniObject);
            }
        }

        public override void SetClient(CallbackCollection callbackCollection, bool SingleConnect)
        {
            _Type = CoreType.Client;
            _Callback = callbackCollection;
            _SingleConnect = SingleConnect;
            _BufferManager = new SimpleBuffer();
            //_BufferManager = new MicrosoftBuffer(4096 * 3, 4096);
            
        }

        public override void SetServer(string ip, int port, CallbackCollection callbackCollection, int MAX_LISTEN)
        {
            _BufferManager = new SimpleBuffer();
            _Callback = callbackCollection;
            //_BufferManager = new MicrosoftBuffer(4096 * 10, 4096);
            for(int i = 0; i < MAX_LISTEN; i++)
            {
                UniNetObject e = new UniNetObject();
                e.SetProtocol(_Callback._ProtocolOp);
                //SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                e.SetCompletedHandler(ProcessIO);
                //args.Completed += new EventHandler<SocketAsyncEventArgs>(ProcessIO);
                //byte[] buf = new byte[4096];
                //_BufferManager.SetBuffer(args.SetBuffer, 4096);
                //_ConnectPool.InitPush(args);
            }
            _IpAddress = ip != null?
                IPAddress.Parse(ip):
                IPAddress.Any;
            _Port = port;
            _ServerUniObject = new UniNetObject();
            _ServerUniObject.SetProtocol(_Callback._ProtocolOp);
            _ServerUniObject.SetRemoteEndPoint(new IPEndPoint(_IpAddress, _Port));
            _MAX_LISTEN = MAX_LISTEN;
            _Callback = callbackCollection;
            _Type = CoreType.Server;
        }

        public override bool StartListen()
        {
            _ServerUniObject = _Callback._ProtocolOp.StartListen(_IpAddress, _Port,_ServerUniObject);
            return true;
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }
    }
}