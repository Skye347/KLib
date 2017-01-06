using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using KLib.NetCore.Error;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;
using System.Text;

namespace KLib.NetCore.Protocol
{
    public class ProtocolOpSsl : ProtocolOpBase
    {
        //private Socket GetRawSocket(UniNetObject connection)
        //{
        //    if (connection.ConnectionType == 0x20)
        //    {
        //        return (connection.innerObject as SslStream).;
        //    }
        //    else if (connection.ConnectionType == 0x21)
        //    {
        //        return (connection.innerObject as SocketAsyncEventArgs).ConnectSocket;
        //    }
        //    else if (connection.ConnectionType == 0x10)
        //    {
        //        return (connection.innerObject as Socket);
        //    }
        //    else
        //    {
        //        return null;
        //    }
        //}
        private SslStream GetRawStream(UniNetObject connection)
        {
            if (connection.ConnectionType == 0x20)
            {
                return (connection.innerObject as SslStream);
            }
            //else if (connection.ConnectionType == 0x21)
            //{
            //    return (connection.innerObject as SocketAsyncEventArgs).ConnectSocket;
            //}
            //else if (connection.ConnectionType == 0x10)
            //{
            //    return (connection.innerObject as Socket);
            //}
            else
            {
                return null;
            }
        }

        public void Write(Byte[] data, UniNetObject connection, out NetCoreException err)
        {
            SslStream sslStream = GetRawStream(connection);
            try
            {
                err = null;
                sslStream.Write(data);
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

        public Socket StartListen(IPAddress ipAddress, int Port)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _Socket.Bind(new IPEndPoint(ipAddress, Port));
            _Socket.Listen(20);
            return _Socket;
        }

        public UniNetObject StartListen(IPAddress ipAddress, int Port, UniNetObject uniObject)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _Socket.Bind(new IPEndPoint(ipAddress, Port));
            _Socket.Listen(20);
            uniObject.stateObject = _Socket;
            return uniObject;
        }

        public Socket GetClient(Socket serverSocket)
        {
            return serverSocket.Accept();
        }

        public bool GetClient(Socket serverSocket, SocketAsyncEventArgs e)
        {
            return serverSocket.AcceptAsync(e);
        }

        public void ConnectAsync(UniNetObject uniObject, IPAddress ipAddress, int Port)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _Socket.Connect(new IPEndPoint(ipAddress, Port));
            var stream = new NetworkStream(_Socket);
            var SslStream = new SslStream(stream, false,
                (object s, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)//验证服务端证书验证是否有错误
                    {
                        return true;
                    }
                    return false;
                    //return true;
                },
                null
            );
            if (ClientCert == null)
            {
                var task = SslStream.AuthenticateAsClientAsync(GetTargetHost(ipAddress));
                task.Wait();
            }
            else
            {
                var task =SslStream.AuthenticateAsClientAsync(GetTargetHost(ipAddress), new X509Certificate2Collection(ClientCert), SslProtocol, false);
                task.Wait();
            }
            uniObject.innerObject = SslStream;
            //var connectArgs = uniObject.innerObject as SocketAsyncEventArgs;
            //if (connectArgs == null)
            //{
            //    return;
            //}
            uniObject.ConnectionType = 0x20;
            //connectArgs.RemoteEndPoint = new IPEndPoint(ipAddress, Port);
            //if (!_Socket.ConnectAsync(connectArgs))
            //{
            //    uniObject.IOCompletedMethod(uniObject);
            //}
            uniObject.IOCompletedMethod(uniObject);
            return;
        }
        public Socket Connect(IPAddress ipAddress, int Port, out SocketException error)
        {
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
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
        public Byte[] Receive(Socket socket, out SocketException err)
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
        public Byte[] ContinueReceive(Socket socket, out SocketException err)
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

        public object GetAsyncObject()
        {
            return new SocketAsyncEventArgs();
        }

        public void SetAsyncCompleted(Action<UniNetObject> callback, object connectObject)
        {
            var args = connectObject as SocketAsyncEventArgs;
            if (args != null)
            {
                args.Completed += new EventHandler<SocketAsyncEventArgs>((object Sender, SocketAsyncEventArgs connectArgs) =>
                {
                    var uniObject = args.UserToken as UniNetObject;
                    if (uniObject.ObjectError == NetCoreError.TimedOut)
                    {
                        return;
                    }
                    uniObject.FreeTimeout();
                    callback(uniObject);
                });
            }
        }

        public void AttachUniAsyncObject(object connectObject, UniNetObject uniObject)
        {
            var args = connectObject as SocketAsyncEventArgs;
            args.UserToken = uniObject;
        }

        public UniNetObject GetUniAsyncObject(object connectObject)
        {
            var args = connectObject as SocketAsyncEventArgs;
            return args.UserToken as UniNetObject;
        }

        public byte[] Receive(UniNetObject uniObject, out NetCoreError err)
        {
            var connectionArgs = uniObject.innerObject as SslStream;
            //if (connectionArgs.SocketError != SocketError.Success)
            //{
            //    err = NetCoreError.SocketError;
            //    return null;
            //}
            //if (connectionArgs.BytesTransferred == 0)
            //{
            //    err = NetCoreError.SocketError;
            //    return null;
            //}
            byte[] buffer = new byte[uniObject.BufferLength];
            Array.Copy(uniObject.Buffer, buffer, uniObject.BufferLength);
            err = NetCoreError.Success;
            return buffer;
        }

        public bool ReceiveAsync(UniNetObject uniObject)
        {
            var connectionArgs = uniObject.innerObject as SocketAsyncEventArgs;
            SslStream sslstrem = GetRawStream(uniObject);
            var task=sslstrem.ReadAsync(uniObject.Buffer, 0, uniObject.Buffer.Length);
            if (task.IsCompleted)
            {
                return false;
            }
            else
            {
                task.ContinueWith(t =>
                {
                    uniObject.BufferLength = task.Result;
                    if (uniObject.BufferLength == 0)
                    {
                        uniObject.ObjectError = NetCoreError.Disconnecting;
                    }
                    if (uniObject.ObjectError == NetCoreError.TimedOut)
                    {
                        return;
                    }
                    uniObject.FreeTimeout();
                    uniObject.IOCompletedMethod(uniObject);
                });
                return true;
            }
        }

        public void CleanAsyncObject(UniNetObject uniObject)
        {
            DisposeAsyncObject(uniObject);
        }

        public void DisposeAsyncObject(UniNetObject uniObject)
        {
            var connectionArgs = uniObject.innerObject as SslStream;
            uniObject.innerObject = null;
            connectionArgs.Dispose();
        }

        public bool AcceptAsync(UniNetObject ServerObject, UniNetObject uniObject)
        {
            uniObject.ConnectionType = 0x20;//async accept
            var socket = ServerObject.stateObject as Socket;
            var Args = uniObject.innerObject as SocketAsyncEventArgs;
            Args.Completed += new EventHandler<SocketAsyncEventArgs>((object sender, SocketAsyncEventArgs args) =>
            {
                uniObject.ObjectError = (NetCoreError)(int)args.SocketError;
            });
            return socket.AcceptAsync(Args);
        }

        public void SetBuffer(UniNetObject uniObject, byte[] buf, int a, int b)
        {
            //var Args = uniObject.innerObject as SocketAsyncEventArgs;
            //Args.SetBuffer(buf, a, b);
        }

        public UniNetObject GetAcceptedUniObject(UniNetObject AcceptObject, ref UniNetObject ClientObject)
        {
            var Args = AcceptObject.innerObject as SocketAsyncEventArgs;
            ClientObject.ConnectionType = 0x20;//async accept
            var AcceptSocket = Args.AcceptSocket;
            var SocketStream = new NetworkStream(AcceptSocket);
            var SslStream = new SslStream(SocketStream);
            var AuthTask=SslStream.AuthenticateAsServerAsync(ServerCert, false, SslProtocol, true);
            AuthTask.Wait();
            ClientObject.innerObject = SslStream;
            //(ClientObject.innerObject as SocketAsyncEventArgs).AcceptSocket = Args.AcceptSocket;
            Args.AcceptSocket = null;
            return ClientObject;
        }

        public System.Security.Authentication.SslProtocols SslProtocol=System.Security.Authentication.SslProtocols.Tls;
        public X509Certificate2 ServerCert = null;
        public X509Certificate2 ClientCert = null;
        public String targetHost;
        public static ProtocolOpSsl BuildProtocolSsl()
        {
            return new ProtocolOpSsl();
        }
        public ProtocolOpSsl SetServerCert(string PFXPath, string PFXPwd)
        {
            ServerCert = new X509Certificate2(PFXPath, PFXPwd);
            return this;
        }
        public ProtocolOpSsl SetClientCert(string PFXPath,string PFXPwd)
        {
            ClientCert = new X509Certificate2(PFXPath, PFXPwd);
            return this;
        }
        public ProtocolOpSsl SetTargetHost(IPAddress IPAddress,string targetHost)
        {
            if (!targetHostByIp.ContainsKey(IPAddress))
            {
                targetHostByIp.Add(IPAddress, targetHost);
            }
            return this;
        }
        public string GetTargetHost(IPAddress IPAddress)
        {
            return targetHostByIp[IPAddress];
        }
        private Dictionary<IPAddress, string> targetHostByIp = new Dictionary<IPAddress, string>();
    }
}