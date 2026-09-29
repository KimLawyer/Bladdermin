using BladderMin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VMS.TPS.Common.Model.API;
using VMS.TPS.Common.Model.Types;
using VMS.TPS;
using System.Xml.Serialization;
using System.Xml;
using System.Data;

namespace BladderMin
{
    public class ProtocolLibraryBuilder
    {
        private readonly List<Protocol_Preprocessor.BladderminProtocol> books;
        private readonly List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>> library;

        public ProtocolLibraryBuilder()
        {
            books = FillLibrary();
            library = SortLibrary(books);
        }

        public List<Protocol_Preprocessor.BladderminProtocol> Books
        {
            get { return books; }
        }
        public List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>> Library
        {
            get { return library; }
        }
        public static List<Protocol_Preprocessor.BladderminProtocol> FillLibrary()
        {
            var books = new List<Protocol_Preprocessor.BladderminProtocol>();
            var eng = new GenerateProtocolEnum();
            var ser = new SerializeProtocolData();
            var count = eng.F1les.Count;
            for (int i = 0; i < count; i++)
            {
                var prot = ser.SerializeProtocol(eng.F1les.ToArray()[i]);
                if (prot != null) // Check if the protocol was successfully deserialized
                    books.Add(prot);
            }
            return books;
        }
        public static List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>> SortLibrary(List<Protocol_Preprocessor.BladderminProtocol> books)
        {
            var library = new List<Tuple<string, Protocol_Preprocessor.BladderminProtocol>>();
            foreach (var book in books)
            {
                var name = book.ProtocolMetaData.Name;
                library.Add(Tuple.Create(name, book));
            }
            return library;
        }
    }
}
