using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using KLib.NetCore.Callback;
using KLib.NetCore.Protocol;
using static KLib.Log.Log;
using System.Threading;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace KLib.NetCore
{
    public class AsynCore : Core
    {
        private SocketAsyncPool _ConnectPool;
        private Socket _ServerSocket;
        private CoreType _Type;
        private BufferManager _BufferManager;
        public override bool Connect(string ip, int port, bool GoAsync = true)
        {
            if (_SingleConnect && _ServerSocket != null)
            {
                return false;
            }
            SocketAsyncEventArgs e = new SocketAsyncEventArgs();
            //byte[] buf = new byte[4096];
            //e.SetBuffer(buf, 0, 4096);
            e.Completed += new EventHandler<SocketAsyncEventArgs>(ProcessIO);
            e.RemoteEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);
            Socket tempServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            e.UserToken = new SocketStateObject(tempServerSocket);
            //_ServerSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            if (!tempServerSocket.ConnectAsync(e))
            {
                ProcessConnected(e);
            }
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

        private void StartAccept(SocketAsyncEventArgs Args)
        {
            if (Args == null)
            {
                Args = new SocketAsyncEventArgs();
                Args.Completed += new EventHandler<SocketAsyncEventArgs>((object sender,SocketAsyncEventArgs AcceptArgs)=> {
                    GetAccepted(AcceptArgs);
                });
            }
            else
            {
                Args.AcceptSocket = null;
            }
            if (!_ServerSocket.AcceptAsync(Args))
            {
                GetAccepted(Args);
            }
        }

        private void GetAccepted(SocketAsyncEventArgs AcceptedArgs)
        {
            if (AcceptedArgs.SocketError != SocketError.Success)
            {
                StartAccept(AcceptedArgs);
                return;
            }
            Socket clientSocket = AcceptedArgs.AcceptSocket;
            SocketException err;
            _Callback.Accepted(clientSocket, out err);
            if (err != null)
            {
                clientSocket.Dispose();
                StartAccept(AcceptedArgs);
                return;
            }
            var clientArgs=_ConnectPool.Pop();
            clientArgs.UserToken=new SocketStateObject(clientSocket);
            if (!clientSocket.ReceiveAsync(clientArgs))
            {
                ProcessReceive(clientArgs);
            }
            StartAccept(AcceptedArgs);
        }

        //[MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private void ProcessConnected(SocketAsyncEventArgs connectionArgs)
        {
            SocketException err;
            var state=_Callback.Connected(connectionArgs.ConnectSocket, out err);
            if (err != null)
            {
                connectionArgs.Dispose();
                return;
            }
            connectionArgs.UserToken = new SocketStateObject(connectionArgs.ConnectSocket,state);
            connectionArgs.SetBuffer(new Byte[4096], 0, 4096);
            if (!connectionArgs.ConnectSocket.ReceiveAsync(connectionArgs))
            {
                ProcessReceive(connectionArgs);
            }
        }

        private void ProcessBadConnection(SocketAsyncEventArgs connectionArgs)
        {
            _Callback.Aborted((connectionArgs.UserToken as SocketStateObject).socket, (connectionArgs.UserToken as SocketStateObject).state);
            connectionArgs.AcceptSocket = null;
            connectionArgs.UserToken = null;
            connectionArgs.RemoteEndPoint = null;
            if (_ConnectPool == null)
            {
                connectionArgs.Dispose();
            }
            else
            {
                _ConnectPool.Push(connectionArgs);
            }
            return;
        }

        private void ProcessReceive(SocketAsyncEventArgs connectionArgs)
        {
            if (connectionArgs.SocketError != SocketError.Success)
            {
                ProcessBadConnection(connectionArgs);
                return;
            }
            if (connectionArgs.BytesTransferred == 0)
            {
                (connectionArgs.UserToken as SocketStateObject).socket.Shutdown(SocketShutdown.Both);
                return;
            }
            byte[] buffer = new byte[connectionArgs.BytesTransferred];
            Socket clientSocket = (connectionArgs.UserToken as SocketStateObject).socket;
            Array.Copy(connectionArgs.Buffer, buffer, connectionArgs.BytesTransferred);
            connectionArgs.Buffer[0] = 0;
            SocketException err;
            var State = _Callback.Received(buffer, clientSocket, out err, (connectionArgs.UserToken as SocketStateObject).state);
            if (err != null)
            {
                ProcessBadConnection(connectionArgs);
                return;
            }
            (connectionArgs.UserToken as SocketStateObject).state = State;
        }

        private void ProcessIO(object sender,SocketAsyncEventArgs connectionArgs)
        {
            if (connectionArgs.LastOperation == SocketAsyncOperation.Accept)
            {
                StartAccept(connectionArgs);
            }
            else if (connectionArgs.LastOperation == SocketAsyncOperation.Receive)
            {
                ProcessReceive(connectionArgs);
                if (connectionArgs.UserToken == null)
                {
                    return;
                    //ProcessBadConnection(connectionArgs)
                }
                Socket clientSocket = (connectionArgs.UserToken as SocketStateObject).socket;
                if (!clientSocket.ReceiveAsync(connectionArgs))
                {
                    ProcessReceive(connectionArgs);
                }
            }
            else if (connectionArgs.LastOperation == SocketAsyncOperation.Connect)
            {
                ProcessConnected(connectionArgs);
            }
        }

        public override void SetClient(CallbackCollection callbackCollection, bool SingleConnect)
        {
            _Type = CoreType.Client;
            _Callback = callbackCollection;
            _SingleConnect = SingleConnect;
        }

        public override void SetServer(string ip, int port, CallbackCollection callbackCollection, int MAX_LISTEN)
        {
            _BufferManager = new BufferManager(4096 * _MAX_LISTEN*2, 4096);
            _BufferManager.InitBuffer();
            _ConnectPool = new SocketAsyncPool();
            _ConnectPool.Init(MAX_LISTEN);
            for(int i = 0; i < MAX_LISTEN; i++)
            {
                SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                args.Completed += new EventHandler<SocketAsyncEventArgs>(ProcessIO);
                byte[] buf = new byte[4096];
                args.SetBuffer(buf, 0, 4096);
                _ConnectPool.InitPush(args);
            }
            _IpAddress = ip != null?
                IPAddress.Parse(ip):
                IPAddress.Any;
            _Port = port;
            _MAX_LISTEN = MAX_LISTEN;
            _Callback = callbackCollection;
            _Type = CoreType.Server;
        }

        public override bool StartListen()
        {
            _ServerSocket = _Callback._ProtocolOp.StartListen(_IpAddress, _Port);
            return true;
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }
    }

    class SocketAsyncPool
    {
        //private int _MAX;
        //private int _Usage;
        public int Usage
        {
            get;
            private set;
        }
        public int MAX
        {
            get;
            private set;
        }
        private Stack<SocketAsyncEventArgs> _pool = new Stack<SocketAsyncEventArgs>();
        private object lockObject = new object();
        public void Init(int Max)
        {
            this.MAX = Max;
            this.Usage = 0;
            //while (true)
            //{
            //    MAX--;
            //    _pool.Push(new SocketAsyncEventArgs());
            //    if (Usage == 0)
            //    {
            //        break;
            //    }
            //}
        }
        public SocketAsyncEventArgs Pop()
        {
            if (Usage == MAX)
            {
                return null;
            }
            SocketAsyncEventArgs ret;
            lock (lockObject)
            {
                Usage++;
                ret=_pool.Pop();
            }
            return ret;
        }
        public void Push(SocketAsyncEventArgs args)
        {
            if (Usage == 0)
            {
                return;
            }
            lock (lockObject)
            {
                Usage--;
                _pool.Push(args);
            }
        }
        public void InitPush(SocketAsyncEventArgs args)
        {
            lock (lockObject)
            {
                Usage--;
                _pool.Push(args);
            }
        }
    }

    class BufferManager
    {
        int m_numBytes;                 // the total number of bytes controlled by the buffer pool
        byte[] m_buffer;                // the underlying byte array maintained by the Buffer Manager
        Stack<int> m_freeIndexPool;     // 
        int m_currentIndex;
        int m_bufferSize;

        public BufferManager(int totalBytes, int bufferSize)
        {
            m_numBytes = totalBytes;
            m_currentIndex = 0;
            m_bufferSize = bufferSize;
            m_freeIndexPool = new Stack<int>();
        }

        // Allocates buffer space used by the buffer pool
        public void InitBuffer()
        {
            // create one big large buffer and divide that 
            // out to each SocketAsyncEventArg object
            m_buffer = new byte[m_numBytes];
        }

        // Assigns a buffer from the buffer pool to the 
        // specified SocketAsyncEventArgs object
        //
        // <returns>true if the buffer was successfully set, else false</returns>
        public bool SetBuffer(SocketAsyncEventArgs args)
        {

            if (m_freeIndexPool.Count > 0)
            {
                args.SetBuffer(m_buffer, m_freeIndexPool.Pop(), m_bufferSize);
            }
            else
            {
                if ((m_numBytes - m_bufferSize) < m_currentIndex)
                {
                    return false;
                }
                args.SetBuffer(m_buffer, m_currentIndex, m_bufferSize);
                m_currentIndex += m_bufferSize;
            }
            return true;
        }

        // Removes the buffer from a SocketAsyncEventArg object.  
        // This frees the buffer back to the buffer pool
        public void FreeBuffer(SocketAsyncEventArgs args)
        {
            m_freeIndexPool.Push(args.Offset);
            args.SetBuffer(null, 0, 0);
        }

    }
}