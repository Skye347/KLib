using System;
using System.Collections.Generic;
using KLib.HTTP;
using System.Collections.Concurrent;
using System.Threading;

namespace KLib.Spider
{
    abstract public class SpiderBase
    {
        public List<string> startList;
        abstract public SpiderRequest Start(SpiderResponse response);
    }

    public class SpiderResponse
    {
        public SpiderResponse(SpiderRequest request, HTTPResponse HttpResponse)
        {
            this.request = request;
            this.httpResponse = HttpResponse;
        }
        public SpiderRequest request { get; private set; }
        public HTTPResponse httpResponse { get; private set; }
        public static SpiderResponse Make(SpiderRequest request, HTTPResponse HttpResponse)
        {
            return new SpiderResponse(request, HttpResponse);
        }
    }

    public class SpiderRequest
    {
        public HTTPMethod method;
        public string Url;
        public Spider.SpiderRequestCallback callback;
        public HTTPCookie Cookie;
        public Dictionary<string, string> AdditionHeader;
        public static SpiderRequest Make(String Url,Spider.SpiderRequestCallback callback)
        {
            SpiderRequest newRequest = new SpiderRequest();
            newRequest.Url = Url;
            newRequest.callback = callback;
            return newRequest;
        }
        public SpiderRequest AddHeader(Dictionary<string, string> Header)
        {
            this.AdditionHeader = Header;
            return this;
        }
        public SpiderRequest AddCookie(HTTPCookie Cookie)
        {
            this.Cookie.AddCookie(Cookie);
            return this;
        }
    }

    public static class Spider
    {
        public delegate SpiderRequest SpiderRequestCallback(SpiderResponse response);
        public static SpiderBase currentSpider { get; private set; }
        public static bool isRunning { get; private set; }
        public static SpiderRequest MakeRequest(string Url, SpiderRequestCallback callback)
        {
            return SpiderRequest.Make(Url, callback);
        }
        public static bool loadSpider(SpiderBase spider)
        {
            if (isRunning)
            {
                return false;
            }
            currentSpider = spider;
            foreach(var item in currentSpider.startList)
            {
                requestList.Enqueue(MakeRequest(item, currentSpider.Start));
            }
            return true;
        }
        private static ConcurrentQueue<SpiderRequest> requestList = new ConcurrentQueue<SpiderRequest>();
        private static object waitLock = new object();
        private static SpiderRequest GetNext()
        {
            SpiderRequest next = null;
            while (isRunning)
            {
                requestList.TryDequeue(out next);
                if (next != null)
                {
                    break;
                }
                else
                {
                    if (isRunning == false)
                    {
                        next = null;
                        break;
                    }
                    lock (waitLock)
                    {
                        Monitor.Wait(waitLock);
                    }
                }
            }
            return next;
        }
        public static void Stop()
        {
            isRunning = false;
            lock (waitLock)
            {
                Monitor.Pulse(waitLock);
            }
        }
        public static HTTPRequest HttpCallback(HTTPResponse response)
        {
            SpiderRequest request = response.request.Addition as SpiderRequest;
            var sResponse = new SpiderResponse(request, response);
            var n = request.callback(sResponse);
            if (!isRunning)
            {
                return null;
            }
            if (n == null)
            {
                return null;
            }
            return response.MakeRequest(n.method, n.Url, n, HttpCallback,n.Cookie,n.AdditionHeader);
        }
        public static void Run()
        {
            HTTPOp.Init();
            isRunning = true;
            SpiderRequest next;
            while (isRunning)
            {
                next = GetNext();
                if (next == null)
                {
                    break;
                }
                if (isRunning)
                {
                    HTTPError err=HTTPOp.Request(next.method, next.Url, next, HttpCallback);
                    if (err != HTTPError.SUCCESS)
                    {
                        Log.Log.log("DNS Error:"+ next.Url, Log.Log.ERROR, "Spider.Run");
                    }
                }
            }
        }
    }
}
