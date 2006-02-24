using System;
using System.Collections;
using System.Xml;

namespace Stetic {

	public abstract class ItemDescriptor {

		Hashtable deps, visdeps;
		bool isInternal;

		protected ItemDescriptor () {}

		protected ItemDescriptor (XmlElement elem, ItemGroup group, ClassDescriptor klass)
		{
			isInternal = elem.HasAttribute ("internal");
			deps = AddSubprops (elem.SelectNodes ("./disabled-if"), group, klass);
			visdeps = AddSubprops (elem.SelectNodes ("./invisible-if"), group, klass);
		}

		Hashtable AddSubprops (XmlNodeList nodes, ItemGroup group, ClassDescriptor klass)
		{
			Hashtable hash = null;

			foreach (XmlElement elem in nodes) {
				string name = elem.GetAttribute ("name");
				string value = elem.GetAttribute ("value");

				PropertyDescriptor prop = (PropertyDescriptor)group[name];
				if (prop == null)
					prop = (PropertyDescriptor)klass[name];
				if (prop == null)
					throw new ArgumentException ("Bad sub-prop " + name);
				if (hash == null)
					hash = new Hashtable ();
				ArrayList values = (ArrayList)hash[prop];
				if (values == null)
					hash[prop] = values = new ArrayList ();

				values.Add (prop.StringToValue (value));
			}
			return hash;
		}

		// The property's display name
		public abstract string Name { get; }

		public bool HasDependencies {
			get {
				return deps != null;
			}
		}

		public bool EnabledFor (object obj)
		{
			if (deps == null)
				return true;

			foreach (PropertyDescriptor dep in deps.Keys) {
				object depValue = dep.GetValue (obj);
				foreach (object value in (ArrayList)deps[dep]) {
					if (value.Equals (depValue))
						return false;
				}
			}
			return true;
		}

		public bool HasVisibility {
			get {
				return visdeps != null;
			}
		}

		public bool VisibleFor (object obj)
		{
			if (visdeps == null)
				return true;

			foreach (PropertyDescriptor dep in visdeps.Keys) {
				object depValue = dep.GetValue (obj);
				foreach (object value in (ArrayList)visdeps[dep]) {
					if (value.Equals (depValue))
						return false;
				}
			}
			return true;
		}

		public bool IsInternal {
			get {
				return isInternal;
			}
		}
	}
}
