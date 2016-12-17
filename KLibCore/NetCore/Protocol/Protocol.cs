using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace KLib.NetCore.Protocol{
	    abstract public class ProtocolOpBase{
        abstract public int Read(out Byte[] buffer,Socket socket,out SocketException err);
        abstract public void Write(Byte[] data,Socket socket,out SocketException err);
        abstract public Socket StartListen(IPAddress ipAddress,int Port);
        abstract public Socket GetClient(Socket serverSocket);
        abstract public Socket Connect(IPAddress ipAddress, int Port,out SocketException err);
        abstract public Socket ConnectAsync(IPAddress ipAddress, int Port);
        abstract public Byte[] Receive(Socket socket,out SocketException err);
        abstract public Byte[] ContinueReceive(Socket socket,out SocketException err);
        abstract public bool GetClient(Socket serverSocket, SocketAsyncEventArgs e);
    }

    public class ProtocolOpTcp:ProtocolOpBase{
        private int _MAX_LISTEN = 20;
        private byte[] _Buffer = new byte[8192];
        override public int Read(out Byte[] buffer,Socket socket,out SocketException err){
            try{
                int length=socket.Receive(_Buffer);
                buffer = new Byte[length];
                Array.Copy(_Buffer, buffer, length);
                err = null;
                return length;
            }
            catch(SocketException e){
                err = e;
                buffer = null;
                return 0;
            }
        }

        override public void Write(Byte[] data,Socket socket,out SocketException err){
            try{
                err = null;
                socket.Send(data);
            }
            catch(SocketException e){
                err = e;
            }
        }

        override public Socket StartListen(IPAddress ipAddress,int Port){
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _Socket.Bind(new IPEndPoint(ipAddress, Port));
            _Socket.Listen(_MAX_LISTEN);
            return _Socket;
        }

        override public Socket GetClient(Socket serverSocket){
            return serverSocket.Accept();
        }

        override public bool GetClient(Socket serverSocket, SocketAsyncEventArgs e)
        {
            return serverSocket.AcceptAsync(e);
        }

        override public Socket ConnectAsync(IPAddress ipAddress,int Port){
            Socket _Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SocketAsyncEventArgs connectArgs = new SocketAsyncEventArgs();
            connectArgs.RemoteEndPoint = new IPEndPoint(ipAddress, Port);
            connectArgs.UserToken = _Socket;
            _Socket.ConnectAsync(connectArgs);
            return _Socket;
        }
        override public Socket Connect(IPAddress ipAddress, int Port,out SocketException error)
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
        override public Byte[] Receive(Socket socket,out SocketException err){
            try{
                int length = socket.Receive(_Buffer);
                Byte[] data = new byte[length];
                Array.Copy(_Buffer, 0, data, 0, length);
                err = null;
                return data;
            }
            catch(SocketException e){
                err = e;
                return null;
            }
        }
        override public Byte[] ContinueReceive(Socket socket,out SocketException err){
            if (socket.Poll(0, SelectMode.SelectRead)){
                SocketException receiveErr;
                byte[] data = Receive(socket,out receiveErr);
                if(receiveErr==null){
                    err = null;
                    return data;
                }
                else{
                    err = receiveErr;
                    return null;
                }
            }
            err = null;
            return null;
        }
    }
}