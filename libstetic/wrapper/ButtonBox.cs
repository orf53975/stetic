using System;
using System.Collections;

namespace Stetic.Wrapper {

	public abstract class ButtonBox : Box {

		public static new Type WrappedType = typeof (Gtk.ButtonBox);

		static new void Register (Type type)
		{
			AddItemGroup (type, "Button Box Properties",
				      "LayoutStyle",
				      "Homogeneous",
				      "Spacing",
				      "BorderWidth");
		}

		public class ButtonBoxChild : Container.ContainerChild {

			public static new Type WrappedType = typeof (Gtk.ButtonBox.ButtonBoxChild);

			static new void Register (Type type)
			{
				ItemGroup props = AddItemGroup (type, "Button Box Child Layout",
								"PackType",
								"Secondary",
								"Position",
								"Expand",
								"Fill",
								"Padding");
				props["Fill"].DependsOn (props["Expand"]);
			}
		}

		public override Widget GladeImportChild (string className, string id, Hashtable props, Hashtable childprops)
		{
			Gtk.ResponseType response = 0;

			string response_id = props["response_id"] as string;
			if (response_id != null) {
				props.Remove ("response_id");
				response = (Gtk.ResponseType)Int32.Parse (response_id);
			}

			Widget wrapper = base.GladeImportChild (className, id, props, childprops);

			if (response == Gtk.ResponseType.Help) {
				Gtk.ButtonBox.ButtonBoxChild bbc = ((Gtk.Container)Wrapped)[((Gtk.Widget)wrapper.Wrapped).Parent] as Gtk.ButtonBox.ButtonBoxChild;
				bbc.Secondary = true;
			}

			// FIXME; need to do something useful with the response_id
			return wrapper;
		}
	}
}
