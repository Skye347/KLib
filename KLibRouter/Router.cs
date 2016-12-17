using System;
using System.Collections.Generic;

namespace KLib.Router
{
    public class RouterCore<TRouterKey,TTarget> 
    {
        public Byte[] Received(Byte[] data){
            var target = TargetParse(data);
            var routerDest = GetRouterDest(target);
            return Received(routerDest, target);
        }
        public Byte[] Received(TRouterKey routerDest,TTarget data){
            return RouterMap[routerDest](data);
        }
        Dictionary<TRouterKey, RouterCallback> RouterMap=new Dictionary<TRouterKey, RouterCallback>();
        public bool RegisterParseMethod(TargetParseDelegate method){
            if(TargetParse!=null){
                return false;
            }
            TargetParse=method;
            return true;
        }
        public bool Add(TRouterKey Key,RouterCallback Callback){
            RouterMap.Add(Key, Callback);
            return true;
        }
        public bool RegisterGetDestMethod(GetRouterDestDelegate method){
            if(GetRouterDest!=null){
                return false;
            }
            GetRouterDest=method;
            return true;
        }
        public Byte[] Goto(TRouterKey Key, TTarget data)
        {
            return RouterMap[Key](data);
        }
        public delegate TTarget TargetParseDelegate(Byte[] data);
        public delegate TRouterKey GetRouterDestDelegate(TTarget target);
        public TargetParseDelegate TargetParse;
        public GetRouterDestDelegate GetRouterDest;
        public delegate byte[] RouterCallback(TTarget Target);
    }
}
