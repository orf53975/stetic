using System;
using System.Collections;
using System.Xml;

namespace Stetic.Wrapper {

	public class RadioMenuItem : MenuItem {

		static RadioGroupManager GroupManager = new RadioGroupManager (typeof (Gtk.RadioMenuItem));

		public override void Wrap (object obj, bool initialized)
		{
			base.Wrap (obj, initialized);

			Gtk.RadioMenuItem radio = (Gtk.RadioMenuItem)Wrapped;
			if (!initialized)
				Group = GroupManager.LastGroup;
			else if (radio.Group == null)
				Group = radio.Name;
		}

		public override void Read (XmlElement elem, FileFormat format)
		{
			string group = (string)GladeUtils.ExtractProperty (elem, "group", "");
			bool active = (bool)GladeUtils.ExtractProperty (elem, "active", false);
			base.Read (elem, format);

			if (group != "")
				Group = group;
			else
				Group = Wrapped.Name;
			if (active)
				((Gtk.RadioMenuItem)Wrapped).Active = true;
		}

		public override XmlElement Write (XmlDocument doc, FileFormat format)
		{
			XmlElement elem = base.Write (doc, format);
			string group = GroupManager.GladeGroupName (Wrapped);
			if (group != Wrapped.Name)
				GladeUtils.SetProperty (elem, "group", group);
			return elem;
		}

		public string Group {
			get {
				return GroupManager[Wrapped];
			}
			set {
				GroupManager[Wrapped] = value;
				EmitNotify ("Group");
			}
		}
	}
}
