
using System;
using Mono.Unix;

namespace Stetic.Editor
{
	public class NonContainerWarningDialog: IDisposable
	{
		[Glade.Widget] Gtk.CheckButton showCheck;
		[Glade.Widget] Gtk.Button linkButton;
		[Glade.Widget] Gtk.Button okbutton;
		[Glade.Widget ("AddNonContainerDialog")] Gtk.Dialog dialog;
		
		public NonContainerWarningDialog()
		{
			Glade.XML xml = new Glade.XML (null, "stetic.glade", "AddNonContainerDialog", null);
			xml.Autoconnect (this);
			
			((Gtk.Label)linkButton.Child).Markup = "<u><span foreground='blue'>" + Catalog.GetString ("GTK# Widget Layout and Packing") + "</span></u>";

			linkButton.Clicked += delegate {
				Gnome.Url.Show ("http://www.mono-project.com/GtkSharp:_Widget_Layout_and_Packing");
			};
			okbutton.HasFocus = true;
		}
		
		public bool ShowAgain {
			get { return !showCheck.Active; }
			set { showCheck.Active = !value; }
		}
		
		public int Run ()
		{
			return dialog.Run ();
		}
		
		public void Dispose ()
		{
			dialog.Destroy ();
		}
	}
}