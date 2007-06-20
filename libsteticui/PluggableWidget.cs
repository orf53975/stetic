
using System;

namespace Stetic
{
	public abstract class PluggableWidget: Gtk.EventBox
	{
		internal Application app;
		bool initialized;
		Gtk.Socket socket;
		bool customWidget;
		Gtk.Notebook book;
		
		public PluggableWidget (Application app)
		{
			book = new Gtk.Notebook ();
			book.ShowTabs = false;
			book.ShowBorder = false;
			book.Show ();
			Add (book);
			
			this.app = app;
			if (app.UseExternalBackend) {
				app.BackendChanged += OnBackendChanged;
				app.BackendChanging += OnBackendChanging;
			}
		}
		
		protected void AddCustomWidget (Gtk.Widget w)
		{
			w.ShowAll ();
			book.AppendPage (w, null);
			book.Page = book.NPages - 1;
			
			if (initialized) {
				Gtk.Widget cw = book.GetNthPage (0);
				book.RemovePage (0);
				cw.Destroy ();
			}
			else
				initialized = true;
			customWidget = true;
		}
		
		protected void ResetCustomWidget ()
		{
			customWidget = false;
		}
		
		protected override void OnRealized ()
		{
			base.OnRealized ();
			if (!initialized && !app.Disposed) {
				initialized = true;
				if (app.UseExternalBackend)
					ConnectPlug ();
				else {
					Gtk.Widget w = OnCreateWidget ();
					w.Show ();
					book.AppendPage (w, null);
				}
			}
		}
		
		protected override void OnUnrealized ()
		{
			if (!app.Disposed && app.UseExternalBackend && initialized) {
				OnDestroyPlug (socket.Id);
				initialized = false;
			}
			base.OnUnrealized ();
		}
		
		protected void PrepareUpdateWidget ()
		{
			// This method is called when the child widget is going to be changed.
			// It takes a 'screenshot' of the widget. This image will be shown until
			// UpdateWidget is called.
			
			if (book.NPages == 1) {
				Gtk.Widget w = book.GetNthPage (0);
				Gdk.Window win = w.GdkWindow;
				Gdk.Pixbuf img = Gdk.Pixbuf.FromDrawable (win, win.Colormap, w.Allocation.X, w.Allocation.Y, 0, 0, w.Allocation.Width, w.Allocation.Height);
				Gtk.Image oldImage = new Gtk.Image (img);
				oldImage.Show ();
				book.AppendPage (oldImage, null);
				book.Page = 1;
				book.RemovePage (0);
			}
		}
		
		protected void UpdateWidget ()
		{
			if (!initialized || app.Disposed)
				return;

			if (!app.UseExternalBackend) {
				Gtk.Widget w = OnCreateWidget ();
				w.Show ();
				book.AppendPage (w, null);
				book.Page = book.NPages - 1;
				if (book.NPages > 1) {
					Gtk.Widget cw = book.GetNthPage (0);
					book.RemovePage (0);
					cw.Destroy ();
				}
			}
		}
		
		protected abstract void OnCreatePlug (uint socketId);
		protected abstract void OnDestroyPlug (uint socketId);
		
		protected abstract Gtk.Widget OnCreateWidget ();
		
		public override void Dispose ()
		{
			if (app.UseExternalBackend) {
				app.BackendChanged -= OnBackendChanged;
				app.BackendChanging -= OnBackendChanging;
			}
			base.Dispose ();
		}
		
		internal virtual void OnBackendChanged (ApplicationBackend oldBackend)
		{
			if (!initialized || app.Disposed)
				return;

			if (app.UseExternalBackend) {
				Gtk.Widget w = book.GetNthPage (0);
				book.RemovePage (0);
				w.Destroy ();
				socket.Dispose ();
				ConnectPlug ();
			}
		}
		
		internal virtual void OnBackendChanging ()
		{
		}
		
		void ConnectPlug ()
		{
			socket = new Gtk.Socket ();
			socket.Show ();
			book.AppendPage (socket, null);
			OnCreatePlug (socket.Id);
		}
	}
}
