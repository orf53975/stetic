using Gtk;
using Gdk;
using System;
using System.Collections;
using System.Reflection;

namespace Stetic {

	public class WidgetFactory : WidgetSite {

		protected Project project;
		protected Type wrapperType;

		public WidgetFactory (Project project, string name, Pixbuf icon, Type wrapperType) : base (project)
		{
			this.project = project;
			this.wrapperType = wrapperType;

			CanFocus = false;

			Gtk.HBox hbox = new HBox (false, 6);

			icon = icon.ScaleSimple (16, 16, Gdk.InterpType.Bilinear);
			hbox.PackStart (new Gtk.Image (icon), false, false, 0);

			Gtk.Label label = new Gtk.Label ("<small>" + name + "</small>");
			label.UseMarkup = true;
			label.Justify = Justification.Left;
			label.Xalign = 0;
			hbox.PackEnd (label, true, true, 0);

			Add (hbox);
		}

		protected override bool StartDrag (Gdk.EventMotion evt)
		{
			Stetic.Wrapper.Widget wrapper = Stetic.ObjectWrapper.Create (project, wrapperType) as Stetic.Wrapper.Widget;
			dragWidget = wrapper.Wrapped as Widget;
			return true;
		}

		protected override void Drop (Widget w, int x, int y)
		{
			;
		}
	}

	public class WindowFactory : WidgetFactory {
		public WindowFactory (Project project, string name, Pixbuf icon, Type wrapperType) :
			base (project, name, icon, wrapperType) {}

		protected override bool OnButtonPressEvent (Gdk.EventButton evt)
		{
			Stetic.Wrapper.Widget wrapper = Stetic.ObjectWrapper.Create (project, wrapperType) as Stetic.Wrapper.Widget;

			Gtk.Window win = wrapper.Wrapped as Gtk.Window;
			win.Present ();

			SteticMain.Project.AddWindow (win);

			WindowSite site = new WindowSite (win);
			SteticMain.Select (site);
			site.FocusChanged += delegate (WindowSite site, IWidgetSite focus) {
				if (focus == null)
					SteticMain.NoSelection ();
				else
					SteticMain.Select (focus);
			};
			return true;
		}

		protected override bool OnMotionNotifyEvent (Gdk.EventMotion evt)
		{
			return true;
		}
	}

}
