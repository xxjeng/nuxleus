﻿using System;
using Nuxleus.Extension.AWS.SimpleDB;
using System.Xml;
using System.Xml.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Web;
using System.Web.Services.Protocols;
using System.Globalization;
using Nuxleus.Extension;
using EeekSoft.Asynchronous;
using System.Collections.Generic;
using System.Threading;
using Nuxleus.MetaData;
using System.Collections;
using System.Xml.Serialization;

namespace Nuxleus.Extension.AWS.SimpleDB {

    public struct SimpleDBService<TRequestType> {

        static XNamespace s = "http://schemas.xmlsoap.org/soap/envelope/";
        static XmlSerializer m_xSerializer = new System.Xml.Serialization.XmlSerializer(typeof(TRequestType));

        //static XElement GetRequestXElement(RequestType requestType, params string[] paramArray) {
        //    switch (requestType) {
        //        case RequestType.Query:
        //            return SdbAction.Query(paramArray[0], paramArray[1], paramArray[2], paramArray[3]);
        //        case RequestType.CreateDomain:
        //            return SdbAction.CreateDomain(paramArray[0]);
        //        case RequestType.DeleteDomain:
        //            return SdbAction.DeleteDomain(paramArray[0]);
        //        case RequestType.ListDomains:
        //            return SdbAction.ListDomains(paramArray[0], paramArray[1]);
        //        case RequestType.GetAttributes:
        //            String[] getattributes = new String[paramArray.Length - 2];
        //            int pa = 0;
        //            int ia = 0;
        //            foreach (String attribute in paramArray) {
        //                if (pa > 1) {
        //                    getattributes[ia] = attribute;
        //                    ia++;
        //                }
        //                pa++;
        //            }
        //            return SdbAction.GetAttributes(paramArray[0], paramArray[1], getattributes);
        //        case RequestType.PutAttributes:
        //            SdbAttribute[] attributes = new SdbAttribute[paramArray.Length - 2];
        //            int p = 0;
        //            int i = 0;
        //            foreach (String attribute in paramArray) {
        //                if (p > 1) {
        //                    attributes[i] = new SdbAttribute { Name = attribute.SubstringBefore("="), Value = attribute.SubstringAfter("=") };
        //                    i++;
        //                }
        //                p++;
        //            }
        //            return SdbAction.PutAttributes(paramArray[0], paramArray[1], attributes);
        //        case RequestType.DeleteAttributes:
        //            SdbAttribute[] delAttributes = new SdbAttribute[paramArray.Length - 2];
        //            int p1 = 0;
        //            int i1 = 0;
        //            foreach (String attribute in paramArray) {
        //                if (p1 > 1) {
        //                    delAttributes[i1] = new SdbAttribute { Name = attribute.SubstringBefore("="), Value = attribute.SubstringAfter("=") };
        //                    i1++;
        //                }
        //                p1++;
        //            }
        //            return SdbAction.DeleteAttributes(paramArray[0], paramArray[1], delAttributes);
        //        default:
        //            return new XElement("null", null);
        //    }
        //}

        public StreamReader MakeRequest(RequestType requestType, XElement message) {

            string rType = LabelAttribute.FromMember(requestType);

            Encoding encoding = new UTF8Encoding();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://sdb.amazonaws.com/");

            request.Timeout = 90000 /*TODO: This should be set dynamically*/;
            request.KeepAlive = true;
            request.Pipelined = true;

            XmlReader xreader = message.CreateReader();
            StringBuilder output = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>");

            byte[] buffer = null;

            do {
                if (xreader.IsStartElement()) {
                    output.Append(xreader.ReadOuterXml());
                    Console.WriteLine("Output: {0} :/Output", output.ToString());
                    buffer = encoding.GetBytes(output.ToString());
                }
            } while (xreader.Read());

            int contentLength = buffer.Length;
            request.ContentLength = contentLength;
            request.Method = "POST";
            request.ContentType = "application/soap+xml";
            request.Headers.Add("SOAPAction", rType);

            try {
                using (Stream newStream = request.GetRequestStream()) {
                    newStream.Write(buffer, 0, contentLength);
                    return new StreamReader(request.GetResponse().GetResponseStream());
                }
            } catch (System.Net.WebException e) {
                Console.WriteLine("Failed! Reason: {0}, Message: {1}", e.Response, e.Message);
                return new StreamReader(e.Response.GetResponseStream());
            }
        }

        public static IEnumerable<IAsync> CallWebService<TRequestType, TResponseType>(ITask task, IRequest sdbRequest, Dictionary<IRequest, TResponseType> responseList) {

            Encoding encoding = new UTF8Encoding();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create("http://sdb.amazonaws.com/");
            request.Timeout = 30000 /*TODO: This should be set dynamically*/;
            request.KeepAlive = true;
            request.Pipelined = false;

            StringBuilder output = new StringBuilder("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            using (XmlReader xreader = CreateSoapMessage(task, LabelAttribute.FromMember(sdbRequest.RequestType)).CreateReader()) {
                while (xreader.Read()) {
                    if (xreader.IsStartElement()) {
                        output.Append(xreader.ReadOuterXml());
                    }
                }
            }

            string soapMessage = output.ToString();
            sdbRequest.RequestMessage = soapMessage;
            Console.WriteLine("XML: {0}", soapMessage);

            byte[] buffer = encoding.GetBytes(soapMessage);

            int contentLength = buffer.Length;
            request.ContentLength = contentLength;
            request.Method = "POST";
            request.ContentType = "application/soap+xml";

            foreach (KeyValuePair<string, string> header in sdbRequest.Headers) {
                request.Headers.Add(header.Key, header.Value);
            }

            System.Console.WriteLine("Start Request: Thread is background: {0}, Thread ID: {1}, Thread is managed: {2}", Thread.CurrentThread.IsBackground, Thread.CurrentThread.ManagedThreadId, Thread.CurrentThread.IsThreadPoolThread);

            using (Stream newStream = request.GetRequestStream()) {
                newStream.Write(buffer, 0, contentLength);
                Async<WebResponse> response = request.GetResponseAsync();
                yield return response;
                System.Console.WriteLine("[] got response on thread: {0}", Thread.CurrentThread.ManagedThreadId);
                Stream stream = response.Result.GetResponseStream();
                Async<TResponseType> responseObject = stream.ReadToEndAsync<TResponseType>().ExecuteAsync<TResponseType>();
                yield return responseObject;
                responseList.Add(sdbRequest, responseObject.Result);
            }
        }

        private static XElement CreateSoapMessage(ITask task, String action) {
            
            StringBuilder builder = new StringBuilder();
            using (TextWriter writer = new StringWriter(builder)) {
                m_xSerializer.Serialize(writer, (TRequestType)task);
            }
            XElement body = XElement.Parse(builder.ToString());
            body.Add(SdbAction.GetAuthorizationElements(action));

            return new XElement(s + "Envelope",
                    new XElement(s + "Body",
                        body
                    )
                );
        }
    }
}
