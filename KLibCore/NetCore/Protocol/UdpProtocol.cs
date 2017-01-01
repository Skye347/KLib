using System;
using System.Net;
using System.Net.Sockets;
using KLib.NetCore.Error;
using System.Collections.Generic;
using System.Text;

namespace KLib.NetCore.Protocol
{
    public class ProtocolOpUdp : ProtocolOpBase
    {
        public bool AcceptAsync(UniNetObject ServerObject, UniNetObject uniObject)
        {
            uniObject.ConnectionType = 0x20;//async accept
            uniObject.LastOperation = UniNetOperation.Receive;
            IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            uniObject.SetRemoteEndPoint(remote);
            var socket = ServerObject.stateObject as Socket;
            var Args = uniObject.innerObject as SocketAsyncEventArgs;
            Args.SetBuffer(new byte[2048], 0, 2048);
            Args.RemoteEndPoint = remote;
            Args.AcceptSocket = socket;
            Args.Completed += new EventHandler<SocketAsyncEventArgs>((object sender, SocketAsyncEventArgs args) =>
            {
                uniObject.ObjectError = (NetCoreError)(int)args.SocketError;
            });
            return socket.ReceiveFromAsync(Args);
            //return false;
        }

        public void AttachUniAsyncObject(object connectObject, UniNetObject uniObject)
        {
            var args = connectObject as SocketAsyncEventArgs;
            args.UserToken = uniObject;
        }

        public void CleanAsyncObject(UniNetObject uniObject)
        {
            var connectionArgs = uniObject.innerObject as SocketAsyncEventArgs;
            connectionArgs.RemoteEndPoint = null;
            connectionArgs.AcceptSocket = null;
        }

        public Socket Connect(IPAddress ipAddress, int Port, out SocketException error)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            SocketAsyncEventArgs connectArgs = new SocketAsyncEventArgs();
            connectArgs.RemoteEndPoint = new IPEndPoint(ipAddress, Port);
            connectArgs.UserToken = _Socket;
            try
            {
                _Socket.Connect(new IPEndPoint(ipAddress, Port));
                error = null;
                return _Socket;
            }
            catch (SocketException e)
            {
                error = e;
                return null;
            }
        }

        public void ConnectAsync(UniNetObject uniObject, IPAddress ipAddress, int Port)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            var connectArgs = uniObject.innerObject as SocketAsyncEventArgs;
            if (connectArgs == null)
            {
                return;
            }
            uniObject.ConnectionType = 0x20;
            connectArgs.RemoteEndPoint = new IPEndPoint(ipAddress, Port);
            connectArgs.AcceptSocket = _Socket;
            if (!_Socket.ConnectAsync(connectArgs))
            {
                uniObject.IOCompletedMethod(uniObject);
            }
            //return connectArgs;
        }

        public byte[] ContinueReceive(Socket socket, out SocketException err)
        {
            if (socket.Poll(0, SelectMode.SelectRead))
            {
                SocketException receiveErr;
                byte[] data = Receive(socket, out receiveErr);
                if (receiveErr == null)
                {
                    err = null;
                    return data;
                }
                else
                {
                    err = receiveErr;
                    return null;
                }
            }
            err = null;
            return null;
        }

        public void DisposeAsyncObject(UniNetObject uniObject)
        {
            var connectionArgs = uniObject.innerObject as SocketAsyncEventArgs;
            uniObject.innerObject = null;
            connectionArgs.Dispose();
        }

        public UniNetObject GetAcceptedUniObject(UniNetObject AcceptObject, ref UniNetObject ClientObject)
        {
            var Args = AcceptObject.innerObject as SocketAsyncEventArgs;
            ClientObject.ConnectionType = 0x20;//async accept
            ClientObject.LastOperation = UniNetOperation.Receive;
            var clientArgs=(ClientObject.innerObject as SocketAsyncEventArgs);
            ClientObject.SetRemoteEndPoint((IPEndPoint)Args.RemoteEndPoint);
            clientArgs.AcceptSocket = Args.AcceptSocket;
            clientArgs.RemoteEndPoint = Args.RemoteEndPoint;
            if (Args.BytesTransferred != 0)
            {
                Array.Copy(Args.Buffer, ClientObject.Buffer, Args.BytesTransferred);
                ClientObject.BufferLength = Args.BytesTransferred;
            }
            return ClientObject;
        }

        public object GetAsyncObject()
        {
            return new SocketAsyncEventArgs();
        }

        public Socket GetClient(Socket serverSocket)
        {
            throw new NotImplementedException();
        }

        public bool GetClient(Socket serverSocket, SocketAsyncEventArgs e)
        {
            throw new NotImplementedException();
        }

        public UniNetObject GetUniAsyncObject(object connectObject)
        {
            var args = connectObject as SocketAsyncEventArgs;
            return args.UserToken as UniNetObject;
        }

        public byte[] Receive(UniNetObject uniObject, out NetCoreError err)
        {
            var connectionArgs = uniObject.innerObject as SocketAsyncEventArgs;
            if (connectionArgs.SocketError != SocketError.Success)
            {
                err = NetCoreError.SocketError;
                return null;
            }
            //if (connectionArgs.BytesTransferred == 0)
            //{
            //    err = NetCoreError.SocketError;
            //    return null;
            //}
            byte[] buffer = new byte[connectionArgs.BytesTransferred];
            Array.Copy(connectionArgs.Buffer, buffer, connectionArgs.BytesTransferred);
            err = NetCoreError.Success;
            return buffer;
        }

        public byte[] Receive(Socket socket, out SocketException err)
        {
            try
            {
                byte[] _Buffer = new byte[4096];
                int length = socket.Receive(_Buffer);
                Byte[] data = new byte[length];
                Array.Copy(_Buffer, 0, data, 0, length);
                err = null;
                return data;
            }
            catch (SocketException e)
            {
                err = e;
                return null;
            }
        }

        public bool ReceiveAsync(UniNetObject uniObject)
        {
            var connectionArgs = uniObject.innerObject as SocketAsyncEventArgs;
            Socket socket = GetRawSocket(uniObject);
            return socket.ReceiveFromAsync(connectionArgs);
        }

        public void SetAsyncCompleted(Action<UniNetObject> callback, object connectObject)
        {
            var args = connectObject as SocketAsyncEventArgs;
            args.Completed += new EventHandler<SocketAsyncEventArgs>((object Sender, SocketAsyncEventArgs connectArgs) =>
            {
                var uniObject = args.UserToken as UniNetObject;
                callback(uniObject);
            });
        }

        public void SetBuffer(UniNetObject uniObject, byte[] buf, int a, int b)
        {
            var Args = uniObject.innerObject as SocketAsyncEventArgs;
            Args.SetBuffer(buf, a, b);
        }

        public Socket StartListen(IPAddress ipAddress, int Port)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _Socket.Bind(new IPEndPoint(ipAddress, Port));
            _Socket.Listen(20);
            return _Socket;
        }

        public UniNetObject StartListen(IPAddress ipAddress, int Port, UniNetObject uniObject)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _Socket.Bind(new IPEndPoint(ipAddress, Port));
            //_Socket.Listen(20);
            uniObject.stateObject = _Socket;
            return uniObject;
        }

        private Socket GetRawSocket(UniNetObject connection)
        {
            if (connection.ConnectionType == 0x20)
            {
                return (connection.innerObject as SocketAsyncEventArgs).AcceptSocket;
            }
            else if (connection.ConnectionType == 0x21)
            {
                return (connection.innerObject as SocketAsyncEventArgs).ConnectSocket;
            }
            else if (connection.ConnectionType == 0x10)
            {
                return (connection.innerObject as Socket);
            }
            else
            {
                return null;
            }
        }

        public void Write(byte[] data, UniNetObject connection, out NetCoreException err)
        {
            Socket socket = GetRawSocket(connection);
            try
            {
                err = null;
                socket.SendTo(data, connection.ipEndPoint);
            }
            catch (SocketException e)
            {
                err = new NetCoreException();
                err.innerException = e;
            }
            catch (NullReferenceException nulle)
            {
                err = new NetCoreException();
                err.innerException = nulle;
            }
        }
    }
}