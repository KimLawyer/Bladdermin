using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace BladderMin
{
    public class XmlTools
    {
        private readonly XmlReaderSettings settings;
        private readonly XmlSerializer serializer;
        public XmlTools()
        {
            settings = new XmlReaderSettings
            {
                // Allow processing of DTD
                DtdProcessing = DtdProcessing.Parse,
                // On older versions of .Net instead set 
                //ProhibitDtd = false,
                // But for security, prevent DOS attacks by limiting the total number of characters that can be expanded to something sane.
                MaxCharactersFromEntities = (long)1e7,
                // And for security, disable resolution of entities from external documents.
                XmlResolver = null,
            };
            serializer = new XmlSerializer(typeof(Protocol_Preprocessor.BladderminProtocol));
        }
        public XmlReaderSettings Settings { get { return settings; } }
        public XmlSerializer Serializer { get { return serializer; } }
    }
}
