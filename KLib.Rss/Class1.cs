using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using System.IO;

namespace KLib.Rss
{
    public class RssBuilder
    {
        public string backupFilePath;
        public static RssDocument AddChannel(RssChannel channel,RssDocument doc,out bool success)
        {
            if (doc.currentChannelsNum + 1 > doc.maxChannelsNum)
            {
                success = false;
                return doc;
            }
            doc.Channels.AddLast(channel);
            success = true;
            return doc;
        }
        public static RssDocument AddItem(string title,string link,string description,string category,RssDocument doc,string ChannelName,bool Append, out bool success)
        {
            RssItem item = new RssItem();
            item.Title = title;
            item.Link = link;
            item.Description = description;
            item.Category = category;
            return AddItem(item, doc, ChannelName, Append,out success);
        }
        public static RssDocument AddItem(RssItem item,RssDocument doc,string ChannelName,bool Append, out bool success)
        {
            int maxItem = -1;
            if (doc.maxItemsPerChannelNum != null)
            {
                maxItem = doc.maxItemsPerChannelNum.Value;
            }
            var channel = (from n in doc.Channels
                          where n.Title == ChannelName
                          select n).ElementAt(0);
            if (channel.maxItemsNum != null)
            {
                maxItem = channel.maxItemsNum.Value;
            }
            if (!Append)
            {
                channel.Items.RemoveLast();
                channel.Items.AddFirst(item);
                success = true;
            }
            else
            {
                if (maxItem == -1)
                {
                    channel.Items.AddFirst(item);
                    channel.currentItemsNum++;
                    success = true;
                }
                else if (channel.maxItemsNum < channel.currentItemsNum + 1)
                {
                    channel.Items.AddFirst(item);
                    channel.currentItemsNum++;
                    success = true;
                }
                else
                {
                    success = false;
                }
            }
            return doc;             
        }
        public static void Build(RssDocument document)
        {
            XDocument doc = new XDocument();
            XElement RssRoot = new XElement("rss",
                new XAttribute("version", "2.0")
                );
            foreach(var Channel in document.Channels)
            {
                XElement channelElement = new XElement("channel",
                    new XElement("title",Channel.Title),
                    new XElement("link",Channel.Link),
                    new XElement("description",Channel.Description)
                    );
                if (Channel.Category != null)
                {
                    channelElement.Add(new XElement("category", Channel.Category));
                }
                foreach(var Item in Channel.Items)
                {
                    XElement ItemElement = new XElement("item",
                        new XElement("title", Item.Title),
                        new XElement("link", Item.Link),
                        new XElement("description", Item.Description)
                    );
                    if (Item.Category != null)
                    {
                        ItemElement.Add(new XElement("category", Item.Category));
                    }
                    channelElement.Add(ItemElement);
                }
                RssRoot.Add(channelElement);
            }
            doc.Add(RssRoot);
            doc.Save(new FileStream(@document.filePath,FileMode.Create));
        }
        public static RssDocument Load(string Path)
        {
            RssDocument rssDoc = new RssDocument();
            rssDoc.Channels = new LinkedList<RssChannel>();
            XDocument doc = XDocument.Load(Path);
            var Channels = from Channel in doc.Descendants("channel")
                           select Channel;
            rssDoc.currentChannelsNum = 0;
            rssDoc.maxChannelsNum = 0;
            rssDoc.maxItemsPerChannelNum = 0;
            foreach(var channelElement in Channels)
            {
                RssChannel channel = new RssChannel();
                channel.Title = channelElement.Element("title").Value;
                channel.Link = channelElement.Element("link").Value;
                channel.Description = channelElement.Element("description").Value;
                channel.Category = channelElement.Element("category").Value;
                channel.Items = new LinkedList<RssItem>();
                var items = from item in channelElement.Descendants("item")
                            select item;
                foreach(var itemElement in items)
                {
                    RssItem item = new RssItem();
                    item.Title = itemElement.Element("title").Value;
                    item.Link = itemElement.Element("link").Value;
                    item.Description = itemElement.Element("description").Value;
                    item.Category = itemElement.Element("category").Value;
                    channel.Items.AddLast(item);
                }
                channel.maxItemsNum = items.Count();
                channel.currentItemsNum = channel.maxItemsNum.Value;
                rssDoc.maxItemsPerChannelNum = (channel.currentItemsNum > rssDoc.maxItemsPerChannelNum.Value ? channel.currentItemsNum : rssDoc.maxItemsPerChannelNum.Value);
                rssDoc.Channels.AddLast(channel);
            }
            rssDoc.maxChannelsNum = Channels.Count();
            rssDoc.currentChannelsNum = rssDoc.maxChannelsNum.Value;
            rssDoc.filePath = Path;
            return rssDoc;
        }
    }

    public class RssDocument
    {
        public int? maxChannelsNum;
        public int? maxItemsPerChannelNum;
        public int currentChannelsNum;
        public string filePath;
        public LinkedList<RssChannel> Channels;
    }

    public class RssChannel
    {
        public int? maxItemsNum;
        public int currentItemsNum;
        public string Title;
        public string Link;
        public string Description;
        public string Category;
        public LinkedList<RssItem> Items;
    }

    public class RssItem
    {
        public string Title;
        public string Link;
        public string Description;
        public string Category;
    }
}
