using System;
using System.Net;
using System.Net.Sockets;
using KLib.NetCore.Error;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;

namespace KLib.NetCore.Protocol{
    public interface ProtocolOpBase
    {
        void Write(Byte[] data, UniNetObject connection, out NetCoreException err);
        Socket StartListen(IPAddress ipAddress, int Port);
        UniNetObject StartListen(IPAddress ipAddress, int Port,UniNetObject uniObject);
        Socket GetClient(Socket serverSocket);
        Socket Connect(IPAddress ipAddress, int Port, out SocketException err);
        Byte[] Receive(Socket socket, out SocketException err);
        Byte[] ContinueReceive(Socket socket, out SocketException err);
        bool GetClient(Socket serverSocket, SocketAsyncEventArgs e);
        object GetAsyncObject();
        void AttachUniAsyncObject(object connectObject, UniNetObject uniObject);
        UniNetObject GetUniAsyncObject(object connectObject);
        SocketAsyncEventArgs ConnectAsync(UniNetObject uniObject, IPAddress ipAddress, int Port);
        void SetAsyncCompleted(Action<UniNetObject> callback, object connectObject);
        byte[] Receive(UniNetObject uniObject,out NetCoreError err);
        bool ReceiveAsync(UniNetObject uniObject);
        bool AcceptAsync(UniNetObject ServerObject,UniNetObject uniObject);
        void CleanAsyncObject(UniNetObject uniObject);
        void DisposeAsyncObject(UniNetObject uniObject);
        void SetBuffer(UniNetObject uniObject, byte[] buf, int a, int b);
        UniNetObject GetAcceptedUniObject(UniNetObject AcceptObject, ref UniNetObject ClientObject );
    }
}