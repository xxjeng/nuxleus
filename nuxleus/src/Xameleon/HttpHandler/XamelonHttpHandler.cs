using System;
using System.IO;
using System.Data;
using System.Configuration;
using System.Threading;
using System.Security.Principal;
using System.Web;
using System.Web.Caching;
using System.Web.Security;
using System.Web.SessionState;
using System.Collections;
using Memcached.ClientLibrary;
using System.Text;
using Saxon.Api;
using System.Xml;
using Xameleon.Configuration;
using Xameleon.Transform;
using System.Collections.Generic;
using Xameleon.Memcached;
using Xameleon.Cryptography;
using Xameleon.Atom;
using Xameleon.Storage;

namespace Xameleon.HttpHandler
{
    public class XameleonHttpHandler : IHttpHandler
    {
      public void ProcessRequest(HttpContext context) {
	HttpRequest req = context.Request;
	HttpResponse resp = context.Response;

	string title = req.Form["title"];
	string location = req.Form["location"];
	DateTime startDate = DateTime.Parse(req.Form["startdate"]);
	DateTime endDate = DateTime.Parse(req.Form["enddate"]);
	string genre = req.Form["genre"];
	string[] tags = req.Form["tags"].Split(',');
	string desc = req.Form["description"];

	AtomEntry entry = new AtomEntry();
	entry.Id = "tag:someid";
	entry.Title = title;
	entry.Published = DateTime.UtcNow;
	entry.Updated = DateTime.UtcNow;
	
	entry.Summary = new TextConstruct(TextElement.Summary);
	entry.Summary.Mediatype = "text";
	entry.Summary.TextContent = desc;

	foreach(string tag in tags) {
	  Category cat = new Category();
	  cat.Term = tag;
	  //entry.Categories.Add(cat);
	}

	ForeignElement point = new ForeignElement("georss", "point", "http://www.georss.org/georss");
	point.Content = location;
	//entry.Foreigns.Add(point);

	string[] coordinates = location.Split(' ');
	ForeignElement Point = new ForeignElement("geo", "Point", "http://www.w3.org/2003/01/geo/wgs84_pos#");
	ForeignElement lat = new ForeignElement("geo", "lat", "http://www.w3.org/2003/01/geo/wgs84_pos#");
	lat.Content = coordinates[0];
	Point.Children.Add(lat);
	ForeignElement lg = new ForeignElement("geo", "long", "http://www.w3.org/2003/01/geo/wgs84_pos#");
	lg.Content = coordinates[1];
	Point.Children.Add(lg);
	//entry.Foreigns.Add(Point);

	ForeignElement startTime = new ForeignElement("llup", "start-time", "http://www.x2x2x.org/llup");
	startTime.Content = startDate.ToString("o");
	//entry.Foreigns.Add(startTime);

	ForeignElement endTime = new ForeignElement("llup", "end-time", "http://www.x2x2x.org/llup");
	endTime.Content = endDate.ToString("o");
	//entry.Foreigns.Add(endTime);

	ForeignElement expires = new ForeignElement("llup", "expires", "http://www.x2x2x.org/llup");
	expires.Content = endDate.ToString("o");
	//entry.Foreigns.Add(expires);

	XmlDocument doc = entry.Document;
	resp.Write(doc.OuterXml);
	
        Client client = (Client)context.Application["memcached"];
	MemcachedStorageResourceInfo memInfo = new MemcachedStorageResourceInfo();
	memInfo.Client = client;
	memInfo.Key = entry.Id;

	StorageResourceInfo info = new StorageResourceInfo();
	info.MemcachedResourceInfo = memInfo;

	Store.Process(info);
      }

      public bool IsReusable {
        get { return false; }
      }
    }
}