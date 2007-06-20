using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Reflection;
using System.Xml;
using Mono.Cecil;

namespace Stetic
{
	internal class CecilWidgetLibrary: WidgetLibrary
	{
		AssemblyDefinition assembly;
		DateTime timestamp;
		string name;
		string fileName;
		XmlDocument objects;
		XmlDocument steticGui;
		IAssemblyResolver resolver;
		string[] dependencies;
		ImportContext importContext;
		bool canGenerateCode;
		Hashtable resolvedCache;
		bool fromCache;
		
		public CecilWidgetLibrary (ImportContext importContext, string assemblyPath)
		{
			ReadCachedDescription (assemblyPath);
			fromCache = objects != null;
	
			this.name = assemblyPath;
			fileName = assemblyPath;
			this.importContext = importContext;
			if (File.Exists (assemblyPath))
				timestamp = System.IO.File.GetLastWriteTime (assemblyPath);
			else
				timestamp = DateTime.MinValue;
			
			ScanDependencies ();
		}
		
		void ReadCachedDescription (string assemblyPath)
		{
			string path = LibraryCache.GetCachedFilePath (assemblyPath);
			if (path == null)
				return;
			
			if (!File.Exists (path + ".objects"))
				return;
			
			try {
				objects = new XmlDocument ();
				objects.Load (path + ".objects");
			}
			catch (Exception ex) {
				Console.WriteLine (ex);
				objects = null;
				return;
			}
			
			if (File.Exists (path + ".steticGui")) {
				try {
					steticGui = new XmlDocument ();
					steticGui.Load (path + ".steticGui");
				}
				catch (Exception ex) {
					Console.WriteLine (ex);
					objects = null;
					steticGui = null;
				}
			}
		}
		
		void StoreCachedDescription ()
		{
			string path = LibraryCache.UpdateCachedFile (fileName);
			if (path == null)
				return;
			
			try {
				string objfile = path + ".objects";
				string guifile = path + ".steticGui";
				
				objects.Save (objfile);
				if (steticGui != null)
					steticGui.Save (guifile);
				else if (File.Exists (guifile))
					File.Delete (guifile);
			}
			catch (Exception ex) {
				Console.WriteLine (ex);
			}
		}
		
		public override string Name {
			get { return name; }
		}
		
		public override bool NeedsReload {
			get {
				if (!System.IO.File.Exists (fileName))
					return false;
				return System.IO.File.GetLastWriteTime (fileName) != timestamp;
			}
		}
		
		public override bool CanReload {
			get { return true; }
		}
		
		public override bool CanGenerateCode {
			get { return canGenerateCode; }
		}
		
		public DateTime TimeStamp {
			get { return timestamp; }
		}
		
		public override void Load ()
		{
			// Assume that it can generate code
			canGenerateCode = true;
			resolvedCache = new Hashtable ();
			
			if (!fromCache) {
				if (assembly == null) {
					if (!File.Exists (fileName)) {
						base.Load (new XmlDocument ());
						return;
					}
					
					timestamp = System.IO.File.GetLastWriteTime (fileName);
					
					assembly = AssemblyFactory.GetAssembly (fileName);
				}
				
				foreach (Resource res in assembly.MainModule.Resources) {
					EmbeddedResource eres = res as EmbeddedResource;
					if (eres == null) continue;
					
					if (eres.Name == "objects.xml") {
						MemoryStream ms = new MemoryStream (eres.Data);
						objects = new XmlDocument ();
						objects.Load (ms);
					}
					
					if (eres.Name == "gui.stetic") {
						MemoryStream ms = new MemoryStream (eres.Data);
						steticGui = new XmlDocument ();
						steticGui.Load (ms);
					}
				}
			}
			
			Load (objects);
			
			if (canGenerateCode) {
				// If it depends on libraries which can't generate code,
				// this one can't
				foreach (string dlib in GetLibraryDependencies ()) {
					WidgetLibrary lib = Registry.GetWidgetLibrary (dlib);
					if (lib != null && !lib.CanGenerateCode) {
						canGenerateCode = false;
						break;
					}
				}
			}
			if (!fromCache) {
				// Store dependencies in the cached xml
				XmlElement elem = objects.CreateElement ("dependencies");
				objects.DocumentElement.AppendChild (elem);
				
				foreach (string dep in dependencies) {
					XmlElement edep = objects.CreateElement ("dependency");
					edep.InnerText = dep;
					elem.AppendChild (edep);
				}
				StoreCachedDescription ();
			}
			
			// This information is not needed after loading
			assembly = null;
			objects = null;
			steticGui = null;
			resolver = null;
			resolvedCache = null;
			fromCache = false;
		}

		protected override ClassDescriptor LoadClassDescriptor (XmlElement element)
		{
			string name = element.GetAttribute ("type");
			
			TypeDefinition cls = null;
			Stetic.ClassDescriptor typeClassDescriptor;
			string tname;
			
			if (!fromCache) {
				cls = assembly.MainModule.Types [name];
				if (cls == null)
					return null;
			
				// Find the nearest type that can be loaded
				typeClassDescriptor = FindType (assembly, cls);
				tname = cls.Name;
				if (typeClassDescriptor != null)
					element.SetAttribute ("baseClassType", typeClassDescriptor.Name);
			}
			else {
				tname = element.GetAttribute ("baseClassType");
				typeClassDescriptor = Stetic.Registry.LookupClassByName (tname);
			}
			
			if (typeClassDescriptor == null) {
				Console.WriteLine ("Descriptor not found: " + tname);
				return null;
			}
			
			XmlElement steticDefinition = null;
			
			if (steticGui != null) {
				string wrappedTypeName = element.GetAttribute ("type");
				steticDefinition = (XmlElement) steticGui.DocumentElement.SelectSingleNode ("widget[@id='" + wrappedTypeName + "']");
			}
			
			CecilClassDescriptor cd = new CecilClassDescriptor (this, element, typeClassDescriptor, steticDefinition, cls);
			
			if (canGenerateCode && !cd.CanGenerateCode)
				canGenerateCode = false;
			return cd;
		}
		
		Stetic.ClassDescriptor FindType (AssemblyDefinition asm, TypeDefinition cls)
		{
			Stetic.ClassDescriptor klass = Stetic.Registry.LookupClassByName (cls.BaseType.FullName);
			if (klass != null) return klass;
			
			TypeDefinition bcls = FindTypeDefinition (cls.BaseType.FullName);
			if (bcls == null)
				return null;

			return FindType (asm, bcls);
		}
		
		AssemblyDefinition ResolveAssembly (AssemblyNameReference aref)
		{
			AssemblyDefinition adef = (AssemblyDefinition) resolvedCache [aref.FullName];
			if (adef != null)
				return adef;
			
			if (resolver == null)
				resolver = new DefaultAssemblyResolver ();
			
			string filePath = null;
			string bpath = Path.Combine (Path.GetDirectoryName (fileName), aref.Name);
			if (File.Exists (bpath + ".dll"))
				filePath = bpath + ".dll";
			if (File.Exists (bpath + ".exe"))
				filePath = bpath + ".exe";
				
			if (filePath != null) {
				adef = AssemblyFactory.GetAssembly (filePath);
			}
			else {
				try {
					adef = resolver.Resolve (aref);
				} catch {
					// If can't resolve, just return null
					return null;
				}
			}
			
			resolvedCache [aref.FullName] = adef;
			return adef;
		}
		
		internal TypeDefinition FindTypeDefinition (string fullName)
		{
			TypeDefinition t = FindTypeDefinition (new Hashtable (), assembly, fullName);
			return t;
		}
		
		TypeDefinition FindTypeDefinition (Hashtable visited, AssemblyDefinition asm, string fullName)
		{
			if (visited.Contains (asm))
				return null;
				
			visited [asm] = asm;
			
			TypeDefinition cls = asm.MainModule.Types [fullName];
			if (cls != null)
				return cls;
			
			foreach (AssemblyNameReference aref in asm.MainModule.AssemblyReferences) {
				AssemblyDefinition basm = ResolveAssembly (aref);
				if (basm != null) {
					cls = basm.MainModule.Types [fullName];
					if (cls != null)
						return cls;
/*					cls = FindTypeDefinition (visited, basm, fullName);
					if (cls != null)
						return cls;
*/				}
			}
			return null;
		}
		
		public override string[] GetLibraryDependencies ()
		{
			if (NeedsReload || dependencies == null)
				ScanDependencies ();
			return dependencies;
		}
		
		void ScanDependencies ()
		{
			if (fromCache) {
				XmlElement elem = objects.DocumentElement ["dependencies"];
				ArrayList list = new ArrayList ();
				foreach (XmlElement dep in elem.SelectNodes ("dependency"))
					list.Add (dep.InnerText);
				dependencies = (string[]) list.ToArray (typeof(string));
			}
			else {
				if (assembly == null) {
					if (!File.Exists (fileName)) {
						dependencies = new string [0];
						return;
					}
					assembly = AssemblyFactory.GetAssembly (fileName);
				}
				ArrayList list = new ArrayList ();
				ScanDependencies (list, assembly);
				dependencies = (string[]) list.ToArray (typeof(string));
			}
		}
		
		void ScanDependencies (ArrayList list, AssemblyDefinition asm)
		{
			string basePath = Path.GetDirectoryName (fileName);
			foreach (AssemblyNameReference aref in asm.MainModule.AssemblyReferences) {
				string file = FindAssembly (importContext, aref.FullName, basePath);
				if (file != null && Application.InternalIsWidgetLibrary (importContext, file))
					list.Add (file);
			}
		}
		
		public static bool IsWidgetLibrary (string path)
		{
			try {
				AssemblyDefinition adef = AssemblyFactory.GetAssembly (path);
				
				foreach (Resource res in adef.MainModule.Resources) {
					EmbeddedResource eres = res as EmbeddedResource;
					if (eres == null) continue;
					if (eres.Name == "objects.xml")
						return true;
				}
			} catch {
			}
			return false;
		}
		
		public static string FindAssembly (ImportContext importContext, string assemblyName, string basePath)
		{
			StringCollection col = new StringCollection ();
			col.Add (basePath);
			if (importContext != null) {
				foreach (string s in importContext.Directories)
					col.Add (s);
			}
			
			AssemblyResolver res = new AssemblyResolver ();
			try {
				return res.Resolve (assemblyName, col);
			} catch {
			}
			return null;
		}

		public static string GetInstanceType (TypeDefinition td, TypeReference sourceType, TypeReference tref)
		{
			string tn = null;
			if (sourceType is GenericInstanceType) {
				GenericInstanceType it = (GenericInstanceType) sourceType;
				foreach (GenericParameter gc in td.GenericParameters) {
					if (gc.Name == tref.FullName) {
						tn = it.GenericArguments [gc.Position].FullName;
						break;
					}
				}
			}
			if (tn == null)
				tn = tref.FullName;
			tn = tn.Replace ('<', '[');
			tn = tn.Replace ('>', ']');
			return tn;
		}
		
		public static List<ComponentType> GetComponentTypes (Application app, string fileName)
		{
			List<ComponentType> list = new List<ComponentType> ();
			AssemblyDefinition asm = AssemblyFactory.GetAssembly (fileName);
			
			EmbeddedResource res = GetResource (asm, "objects.xml");
			if (res == null)
				return list;
				
			MemoryStream ms = new MemoryStream (res.Data);
			XmlDocument objects = new XmlDocument ();
			objects.Load (ms);
			
			foreach (XmlElement elem in objects.SelectNodes ("objects/object")) {
				if (elem.GetAttribute ("internal") == "true" || elem.HasAttribute ("deprecated"))
					continue;
					
				string iconname = elem.GetAttribute ("icon");
				Gdk.Pixbuf icon = null;
				if (iconname.Length > 0) {
					try {
						// Using the pixbuf resource constructor generates a gdk warning.
						res = GetResource (asm, iconname);
						Gdk.PixbufLoader loader = new Gdk.PixbufLoader (res.Data);
						icon = loader.Pixbuf;
					} catch {
						// Ignore
					}
				}
				
				if (icon == null) {
					ClassDescriptor cc = Registry.LookupClassByName ("Gtk.Bin");
					icon = cc.Icon;
				}
				
				ComponentType ct = new ComponentType (app,
					elem.GetAttribute ("type"),
					elem.GetAttribute ("label"), 
					elem.GetAttribute ("type"),
					elem.GetAttribute ("palette-category"), 
					icon);
					
				list.Add (ct);
			}
			
			return list;
		}
		
		static EmbeddedResource GetResource (AssemblyDefinition asm, string name)
		{
			foreach (Resource res in asm.MainModule.Resources) {
				EmbeddedResource eres = res as EmbeddedResource;
				if (eres != null && eres.Name == name)
					return eres;
			}
			return null;
		}
	}
}
