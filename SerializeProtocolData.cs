using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using System.Windows;

namespace BladderMin
{
    //Create protocol list and activate data after selecting protocol
    public class SerializeProtocolData
    {
        //public readonly string path = $@"\\sdappvimg010\esapi$\klawyer\BladderMin\XML Protocol Library\";
        public List<string> ReadFolder()
        {
            return Directory.GetFiles(configPath()).ToList();
        }
        public Protocol_Preprocessor.BladderminProtocol SerializeProtocol(string file)
        {
            var xmlTools = new XmlTools();
            var serializer = xmlTools.Serializer;
            try
            {
                using (var stream = new StreamReader(file))
                {
                    using (var reader = XmlReader.Create(stream, xmlTools.Settings))
                    {
                        var deserializeFile = (Protocol_Preprocessor.BladderminProtocol)serializer.Deserialize(reader);
                        return deserializeFile;
                    }
                }
            }
            catch (Exception ex) //if we can't read a protocol, it's best to crash out instead of locking the main thread
            {
                //throw new Exception($@"{ex.Message}");
                MessageBox.Show($"Error reading protocol file {file} \r\n {ex.Message} \r\n {ex.InnerException} \r\n {ex.StackTrace}", "Error loading protocol", MessageBoxButton.OK, MessageBoxImage.Error);
                return null; //return null if the protocol can't be read
            }
        }
        public string configPath()
        {
            string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string configFile = $@"{directory}\Configuration.xml";
            var loadConfig = XDocument.Load(configFile);

            //Path to protocols.
            string protocolLibraryFolder = loadConfig.Element("Config").Element("Path").Element("Protocols").Value;
            return protocolLibraryFolder;
        }
    
    }
}
