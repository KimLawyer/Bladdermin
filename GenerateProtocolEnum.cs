using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Windows.Navigation;
using BladderMin;
using System.Reflection.Emit;
using System.Reflection;

namespace VMS.TPS
{
    public class GenerateProtocolEnum
    {
        private readonly List<string> files;
        private readonly Type protoEnum;
        public List<string> Files()
        {
            SerializeProtocolData data = new SerializeProtocolData();
            var files = data.ReadFolder();
            return files;
        }
        public List<string> Formater(List<string> inputFiles)
        {
            var decodeFiles = from file in inputFiles
                              select Path.GetFileNameWithoutExtension(file);
            return decodeFiles.ToList();
        }
        public GenerateProtocolEnum()
        {
            var files = Files();
            this.files = files;
            var filesFormatted = Formater(files).ToArray();

            // Get the current application domain for the current thread.
            AppDomain currentDomain = AppDomain.CurrentDomain;

            // Create a dynamic assembly in the current application domain,
            // and allow it to be executed and saved to disk.
            AssemblyName aName = new AssemblyName("TempAssembly");
            AssemblyBuilder ab = currentDomain.DefineDynamicAssembly(
                aName, AssemblyBuilderAccess.RunAndSave);

            // Define a dynamic module in "TempAssembly" assembly. For a single-
            // module assembly, the module has the same name as the assembly.
            ModuleBuilder mb = ab.DefineDynamicModule(aName.Name, aName.Name + ".dll");

            // Define a public enumeration with the name "BladderMinProtocolTypesExpandable" and an
            // underlying type of Integer.
            EnumBuilder eb = mb.DefineEnum("BladderMinProtocolTypesExpandable", TypeAttributes.Public, typeof(int));

            // Define members.
            for (int i = 0; i < filesFormatted.Count(); i++)
            {
                eb.DefineLiteral(filesFormatted[i].ToString(), i);
            }

            // Create the type and save the assembly.
            Type finished = eb.CreateType();
            //ab.Save(aName.Name + ".dll");

            protoEnum = finished;
        }
        public List<string> F1les
        {
            get { return files; }
        }
        public Type ProtoEnum
        {
            get { return protoEnum; }
        }
    }
}
