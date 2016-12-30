using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using KLib.NetCore.Protocol;
using KLib.NetCore;

namespace KLib.NetCore.Callback{
	public class CallbackCollection{
        public void BindCore(KLib.NetCore.Core core){
            currentCore = core;
        }
        public KLib.NetCore.Core currentCore;
        internal ProtocolOpBase _ProtocolOp;
        public bool isInstalled = false;
        public void UseTcp(){
            _ProtocolOp = new ProtocolOpTcp();
            isInstalled = true;
        }

	    public void UseUdp()
	    {
            _ProtocolOp = new ProtocolOpUdp();
            isInstalled = true;
        }
        public void UseCustom(ProtocolOpBase Protocal){
            _ProtocolOp = Protocal;
            isInstalled = true;
        }
        public void NewConnection(string ip,int port){
            currentCore.Connect(ip, port);
        }
        public void StopCore(){
            currentCore.Stop();
        }
        virtual public object Received(byte[] data,UniNetObject connection,out NetCore.Error.NetCoreException err,object Addition=null){
            err = null;
            return null;
        }
        virtual public void Aborted(UniNetObject connection, object Addition)
        {

        }
        //public void ThreadReceived(Object Param){
        //    bool isContinue=Received(((CoreThreadPassObj)Param).data,((CoreThreadPassObj)Param).socket,null);
        //    SocketException receiveErr=null;
        //    while(isContinue){
        //        byte[] data = _ProtocolOp.ContinueReceive(((CoreThreadPassObj)Param).socket,out receiveErr);
        //        if(data==null){
        //            continue;
        //        }
        //        if(data.Length==0&&((CoreThreadPassObj)Param).socket.Available==0){
        //            break;
        //        }
        //        if(receiveErr!=null){
        //            break;
        //        }
        //        isContinue=Received(data, ((CoreThreadPassObj)Param).socket,receiveErr);
        //    }
        //    ((CoreThreadPassObj)Param).socket.Dispose();
        //    ((CoreThreadPassObj)Param).socket = null;
        //}
        //virtual public void Write(Byte[] data, UniNetObject connection, out NetCore.Error.NetCoreException err){
        //    _ProtocolOp.Write(data, connection, out err);
        //}
        virtual public object Connected(UniNetObject connection, out NetCore.Error.NetCoreException err){
            err = null;
            return null;
        }
        virtual public void Accepted(UniNetObject connection, out NetCore.Error.NetCoreException err)
        {
            err = null;
        }
    }
}