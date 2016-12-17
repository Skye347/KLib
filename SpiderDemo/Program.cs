using System;
using KLib.Spider;
using System.Collections.Generic;
using System.IO;
using KLib.Rss;

class Program
{
    static void Main(string[] args)
    {
        //RssDocument doc = new RssDocument();
        //doc.filePath = "D:\\testRss.xml";
        //RssBuilder.Build(doc);
        Spider.loadSpider(new SpiderDemo());
        Spider.Run();
        Console.ReadLine();
    }
}

class SpiderDemo : SpiderBase
{
    private int Count=0;
    public SpiderDemo()
    {
        startList = new List<string>
        {
            "http://httpbin.org/image"
        };
    }
    public override SpiderRequest Start(SpiderResponse response)
    {
        Count++;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
            var bw = new BinaryWriter(new FileStream(@"D:\test.png", FileMode.Create));
            bw.Write(response.httpResponse.binaryData);
        //Console.WriteLine(Count.ToString()+":"+response.request.Url+":"+response.httpResponse.body);
        //if (Count == 2)
        //{
        //    Spider.Stop();
        //    return null;
        //}
        //return SpiderRequest.Make("http://www.oschina.net/", Start);
        return null;
    }
}