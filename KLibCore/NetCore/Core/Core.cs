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
        private volatile int _ObjectError=(int)Error.NetCoreError.Success;
        public NetCore.Error.NetCoreError ObjectError
        {
            get
            {
                return (NetCore.Error.NetCoreError)_ObjectError;
            }
            set
            {
                Interlocked.Exchange(ref _ObjectError, (int)value);
            }
        }
        public byte[] Buffer;
        public int BufferLength=0;
        public object innerObject;
        public object stateObject;
        private ProtocolOpBase protocol;
        public IPEndPoint ipEndPoint;
        public int timeout=500;
        public long CompleteTime;
        private object TimeoutLock=new object();
        public Action<UniNetObject> IOCompletedMethod;
        public Action<UniNetObject> TimeoutMethod;
        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        System.Threading.Timer timer;
        internal void ConnectAsync()
        {
            LastOperation = UniNetOperation.Connect;
            protocol.ConnectAsync(this, ipEndPoint.Address, ipEndPoint.Port);
            if(ObjectError!= Error.NetCoreError.IOPending)
            {
                ObjectError = Error.NetCoreError.IOPending;
                StartTimeoutAsync();
            }
        }
        public void Close()
        {
            ObjectError = Error.NetCoreError.Disconnecting;
        }
        public delegate void TimeoutCallback(UniNetObject uniObject);
        internal void StartTimeoutAsync()
        {
            //if (timer != null)
            //{
            //    FreeTimeout();
            //}
            //log("start timeout", INFO, "StartTimeoutAsync");
            sw.Reset();
            sw.Start();
            timer = new Timer((object a)=> {
                var uniObject = a as UniNetObject;
                if (uniObject.ObjectError == Error.NetCoreError.Success)
                {
                    return;
                }
                else
                {
                    uniObject.ObjectError = Error.NetCoreError.TimedOut;
                    uniObject.CompleteTime = timeout;
                    uniObject.TimeoutMethod(uniObject);
                    return;
                }
            }, this,timeout,Timeout.Infinite);
        }
        public void FreeTimeout()
        {
            if (timer != null)
            {
                //log("stop timeout", INFO, "StartTimeoutAsync");
                CompleteTime = sw.ElapsedMilliseconds;
                timer.Dispose();
            }
            ObjectError = Error.NetCoreError.Success;
            //lock (TimeoutLock)
            //{
            //    Monitor.Pulse(TimeoutLock);
            //}
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
        internal void SetTimeoutHandler(Action<UniNetObject> processTimeout)
        {
            TimeoutMethod = processTimeout;
        }

        internal byte[] ReceiveAll()
        {
            NetCore.Error.NetCoreError err;
            var data = protocol.Receive(this, out err);
            if (err != Error.NetCoreError.Success)
            {
                //log("NetCoreError", ERROR, "UniAsynCore.ReceiveAll");
                return null;
            }
            return data;
        }

        internal bool ReceiveAsync(UniNetObject uniObject)
        {
            ObjectError = Error.NetCoreError.IOPending;
            if (protocol.ReceiveAsync(this))
            {
                StartTimeoutAsync();
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            FreeTimeout();
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
