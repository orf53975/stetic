using System;

namespace Stetic.Wrapper {

	[ObjectWrapper ("Label", "label.png", ObjectWrapperType.Widget)]
	public class Label : Misc {

		public static ItemGroup LabelProperties;

		static Label () {
			LabelProperties = new ItemGroup ("Label Properties",
							 typeof (Gtk.Label),
							 "LabelProp",
							 "UseMarkup",
							 "UseUnderline",
							 "Wrap",
							 "MnemonicWidget",
							 "Justify",
							 "Selectable");

			groups = new ItemGroup[] {
				LabelProperties, Misc.MiscProperties,
				Stetic.Wrapper.Widget.CommonWidgetProperties
			};
		}

		public Label (IStetic stetic) : this (stetic, new Gtk.Label ()) {}

		public Label (IStetic stetic, Gtk.Label label) : base (stetic, label)
		{
			label.LabelProp = label.Name;
		}

		static ItemGroup[] groups;
		public override ItemGroup[] ItemGroups { get { return groups; } }
	}
}