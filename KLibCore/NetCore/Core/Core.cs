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
    public abstract class Core
    {
        abstract public void SetClient(CallbackCollection callbackCollection, bool SingleConnect);
        abstract public void SetServer(String ip, int port, CallbackCollection callbackCollection, int MAX_LISTEN=-1);
        abstract public bool StartListen();
        abstract public bool Connect(String ip, int port,bool GoAsync=false);
        abstract public void Run();
        abstract public void Stop();
        protected IPAddress _IpAddress;
        protected int _Port,_MAX_LISTEN;
        protected CallbackCollection _Callback;
        protected bool _SingleConnect;
    }

    public class CoreThreadPassObj
    {
        public byte[] data;
        public Socket socket;
    }

    public enum CoreType
    {
        Server,
        Client
    }

    public class SocketStateObject
    {
        public SocketStateObject(Socket socket, object data = null)
        {
            this.socket = socket;
            state = data;
        }
        public Socket socket;
        public object state;
    }

    public class AddressHelper
    {
        public static IPAddress HostToIPAddress(string Host)
        {
            var task = System.Net.Dns.GetHostAddressesAsync(Host);
            try
            {
                task.Wait();
                IPAddress ipAddress = task.Result[0];
                return ipAddress;
            }
            catch
            {
                return null;
            }
        }
    }

    public enum UniNetOperation
    {
        Accept,
        Connect,
        Receive
    }

    public class UniNetObject : IDisposable
    {
        public int ConnectionType;
        public UniNetOperation LastOperation;
        public NetCore.Error.NetCoreError ObjectError;
        public byte[] Buffer;
        public int BufferLength=0;
        public object innerObject;
        public object stateObject;
        private ProtocolOpBase protocol;
        public IPEndPoint ipEndPoint;
        public Action<UniNetObject> IOCompletedMethod;
        internal void ConnectAsync()
        {
            LastOperation = UniNetOperation.Connect;
            protocol.ConnectAsync(this, ipEndPoint.Address, ipEndPoint.Port);
        }

        internal void SetProtocol(ProtocolOpBase protocolOp)
        {
            protocol = protocolOp;
            innerObject = protocol.GetAsyncObject();
            protocol.AttachUniAsyncObject(innerObject, this);
        }

        internal void SetRemoteEndPoint(IPEndPoint iPEndPoint)
        {
            this.ipEndPoint = iPEndPoint;
        }

        internal void SetCompletedHandler(Action<UniNetObject> processIO)
        {
            protocol.SetAsyncCompleted(processIO, innerObject);
            IOCompletedMethod = processIO;
        }

        internal byte[] ReceiveAll()
        {
            NetCore.Error.NetCoreError err;
            var data = protocol.Receive(this, out err);
            if (err != Error.NetCoreError.Success)
            {
                log("NetCoreError", ERROR, "UniAsynCore.ReceiveAll");
            }
            return data;
        }

        internal bool ReceiveAsync(UniNetObject uniObject)
        {
            return protocol.ReceiveAsync(this);
        }

        public void Dispose()
        {
            protocol.DisposeAsyncObject(this);
        }

        internal bool AcceptAsync(UniNetObject uniObject)
        {
            return protocol.AcceptAsync(this, uniObject);
        }

        public void SetBuffer(byte[] buf, int a, int b)
        {
            Buffer = buf;
            protocol.SetBuffer(this, buf, a, b);
        }

        public void Write(byte[] buf,out NetCore.Error.NetCoreException err)
        {
            protocol.Write(buf, this, out err);
        }
    }
}
