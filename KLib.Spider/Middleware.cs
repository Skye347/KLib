using System;
using KLib.Spider;
using static KLib.Log.Log;

namespace KLib.Spider.Middleware
{
    public abstract class RequestMiddlewareBase
    {
        public string name;
        public abstract SpiderRequest Process(SpiderRequest request);
        public override bool Equals(object obj)
        {
            var comp = obj as RequestMiddlewareBase;
            if (comp == null)
            {
                return false;
            }
            else
            {
                return comp.name == this.name;
            }
        }
    }
    public abstract class ResponseMiddlewareBase
    {
        public string name;
        public abstract SpiderResponse Process(SpiderResponse response);
        public override bool Equals(object obj)
        {
            var comp = obj as ResponseMiddlewareBase;
            if (comp == null)
            {
                return false;
            }
            else
            {
                return comp.name == this.name;
            }
        }
    }
    public class ExampleRequestMiddleware : RequestMiddlewareBase
    {
        new string name = "ExampleRequestMiddleware";
        public override SpiderRequest Process(SpiderRequest request)
        {
            log("get request:"+request.Url, INFO, "ExampleRequestMiddleware");
            return request;
        }
    }
    public class ExampleResponseMiddleware : ResponseMiddlewareBase
    {
        new string name = "ExampleResponseMiddleware";
        public override SpiderResponse Process(SpiderResponse response)
        {
            log("get response:" + response.httpResponse.status["StatusCode"], INFO, "ExampleRequestMiddleware");
            return response;
        }
    }
}