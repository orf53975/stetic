using System;
using System.Collections;
using System.Reflection;

namespace Stetic.Editor {

	public class Enumeration : Gtk.HBox {

		Gtk.ComboBox combo;
		ArrayList values;

		public Enumeration (PropertyInfo info) : base (false, 0)
		{
			combo = Gtk.ComboBox.NewText ();
			combo.Changed += combo_Changed;
			combo.Show ();
			PackStart (combo, true, true, 0);

			values = new ArrayList ();
			foreach (int i in Enum.GetValues (info.PropertyType)) {
				Enum value = (Enum)Enum.ToObject (info.PropertyType, i);
				string name = Enum.GetName (info.PropertyType, value);

				combo.AppendText (name);
				values.Add (value);
			}
		}

		public Enum Value {
			get {
				return (Enum)values[combo.Active];
			}
			set {
				int i = values.IndexOf (value);
				if (i != -1)
					combo.Active = i;
			}
		}

		public event EventHandler ValueChanged;

		void combo_Changed (object o, EventArgs args)
		{
			if (ValueChanged != null)
				ValueChanged (this, EventArgs.Empty);
		}
	}
}