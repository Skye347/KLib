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
}
