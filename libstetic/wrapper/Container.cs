using System;
using System.Collections;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Stetic.Wrapper {
	public abstract class Container : Widget {

		public static new Type WrappedType = typeof (Gtk.Container);

		static Hashtable childTypes = new Hashtable ();

		static new void Register (Type type)
		{
			// Check if the type or one of its ancestors declares a
			// Stetic.Wrapper.Container.ContainerChild subtype
			Type childType = typeof (Stetic.Wrapper.Container.ContainerChild);

			do {
				foreach (Type ct in type.GetNestedTypes (BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
					if (ct.IsSubclassOf (childType)) {
						if (!childTypes.ContainsValue (ct))
							Stetic.ObjectWrapper.Register (ct);
						childTypes[type] = ct;
						return;
					}
				}
				type = type.BaseType;
			} while (type != typeof (Stetic.Wrapper.Container));
		}

		public override void Wrap (object obj, bool initialized)
		{
			base.Wrap (obj, initialized);
			container.Removed += ChildRemoved;
			container.SizeAllocated += SizeAllocated;
		}

		Gtk.Container container {
			get {
				return (Gtk.Container)Wrapped;
			}
		}

		int freeze;
		protected void Freeze ()
		{
			freeze++;
		}

		protected void Thaw ()
		{
			if (--freeze == 0)
				Sync ();
		}

		protected virtual void DoSync ()
		{
			;
		}

		protected void Sync ()
		{
			if (freeze > 0)
				return;
			freeze = 1;
			DoSync ();
			freeze = 0;
		}

		public virtual Widget GladeImportChild (string className, string id,
							Hashtable props, Hashtable childprops)
		{
			ObjectWrapper wrapper = Stetic.ObjectWrapper.GladeImport (stetic, className, id, props);
			Gtk.Widget child = (Gtk.Widget)wrapper.Wrapped;

			AutoSize[child] = false;
			container.Add (child);

			GladeUtils.SetPacking (container, child, childprops);
			return (Widget)wrapper;
		}

		public virtual void GladeExportChild (Widget wrapper, out string className,
						      out string internalId, out string id,
						      out Hashtable props,
						      out Hashtable childprops)
		{
			internalId = null;
			childprops = null;
			wrapper.GladeExport (out className, out id, out props);

			if (InternalChildId != null)
				internalId = InternalChildId;
			else {
				ObjectWrapper childwrapper = ChildWrapper (wrapper);
				if (childwrapper != null)
					GladeUtils.GetProps (childwrapper, out childprops);
			}
		}

		public virtual Placeholder AddPlaceholder ()
		{
			Placeholder ph = CreatePlaceholder ();
			container.Add (ph);
			return ph;
		}

		public virtual void Add (Gtk.Widget child)
		{
			container.Add (child);
		}

		Gtk.Widget FindInternalChild (string childId)
		{
			Container ancestor = this;
			while (ancestor != null) {
				foreach (Gtk.Widget w in ancestor.container.Children) {
					Widget wrapper = Lookup (w);
					if (wrapper != null && wrapper.InternalChildId == childId)
						return w;
				}
				ancestor = ParentWrapper;
			}
			return null;
		}

		public virtual Widget GladeSetInternalChild (string childId, string className, string id, Hashtable props)
		{
			Gtk.Widget widget = FindInternalChild (childId);
			if (widget == null)
				throw new GladeException ("Unrecognized internal child name", className, false, "internal-child", childId);

			ObjectWrapper wrapper = Stetic.ObjectWrapper.Create (stetic, className);
			GladeUtils.ImportWidget (stetic, wrapper, widget, id, props);

			return (Widget) wrapper;
		}

		public static new Container Lookup (GLib.Object obj)
		{
			return Stetic.ObjectWrapper.Lookup (obj) as Stetic.Wrapper.Container;
		}

		public static Stetic.Wrapper.Container.ContainerChild ChildWrapper (Stetic.Wrapper.Widget wrapper) {
			Stetic.Wrapper.Container parentWrapper = wrapper.ParentWrapper;
			if (parentWrapper == null)
				return null;

			Gtk.Container parent = parentWrapper.Wrapped as Gtk.Container;
			if (parent == null)
				return null;

			Gtk.Widget child = (Gtk.Widget)wrapper.Wrapped;
			while (child != null && child.Parent != parent)
				child = child.Parent;
			if (child == null)
				return null;

			Type ct = null;
			for (Type t = parentWrapper.GetType (); t != null; t = t.BaseType) {
				ct = childTypes[t] as Type;
				if (ct != null)
					break;
			}
			if (ct == null)
				return null;

			Gtk.Container.ContainerChild cc = parent[child];
			return Stetic.ObjectWrapper.Create (parentWrapper.stetic, ct, cc) as ContainerChild;
		}

		public delegate void ContentsChangedHandler (Container container);
		public event ContentsChangedHandler ContentsChanged;

		protected void EmitContentsChanged ()
		{
			if (ContentsChanged != null)
				ContentsChanged (this);
			if (ParentWrapper != null)
				ParentWrapper.ChildContentsChanged (this);
		}

		protected Set AutoSize = new Set ();

		protected virtual Placeholder CreatePlaceholder ()
		{
			Placeholder ph = new Placeholder ();
			ph.Show ();
			ph.Drop += PlaceholderDrop;
			ph.DragEnd += PlaceholderDragEnd;
			ph.ButtonPressEvent += PlaceholderButtonPress;
			AutoSize[ph] = true;
			return ph;
		}

		void PlaceholderButtonPress (object obj, Gtk.ButtonPressEventArgs args)
		{
			if (args.Event.Type != Gdk.EventType.ButtonPress)
				return;

			Placeholder ph = obj as Placeholder;

			if (args.Event.Button == 1) {
				Select (ph);
				args.RetVal = true;
			} else if (args.Event.Button == 3) {
				stetic.PopupContextMenu (ph);
				args.RetVal = true;
			}
		}

		void PlaceholderDrop (Placeholder ph, Gtk.Widget dropped)
		{
			ReplaceChild (ph, dropped);
			ph.Destroy ();
			Stetic.Wrapper.Widget wrapper = Stetic.Wrapper.Widget.Lookup (dropped);
			if (wrapper != null)
				wrapper.Select ();
			EmitContentsChanged ();
		}

		void PlaceholderDragEnd (object obj, Gtk.DragEndArgs args)
		{
			Placeholder ph = obj as Placeholder;

			dragSource = null;
			if (DND.DragWidget == null) {
				ph.SetSizeRequest (-1, -1);
				Sync ();
			} else
				ReplaceChild (ph, DND.DragWidget);
		}

		protected virtual void ChildContentsChanged (Container child) {
			;
		}

		void ChildRemoved (object obj, Gtk.RemovedArgs args)
		{
			ChildRemoved (args.Widget);
		}

		protected virtual void ChildRemoved (Gtk.Widget w)
		{
			AutoSize[w] = false;
			EmitContentsChanged ();
		}

		class RealChildEnumerator {
			public ArrayList Children = new ArrayList ();
			public void Add (Gtk.Widget widget)
			{
				if (!(widget is Placeholder))
					Children.Add (widget);
			}
		}

		public IEnumerable RealChildren {
			get {
				RealChildEnumerator rce = new RealChildEnumerator ();
				container.Forall (rce.Add);
				return rce.Children;
			}
		}

		protected virtual void ReplaceChild (Gtk.Widget oldChild, Gtk.Widget newChild)
		{
			Gtk.Container.ContainerChild cc;
			Hashtable props = new Hashtable ();

			cc = container[oldChild];
			foreach (PropertyInfo pinfo in cc.GetType ().GetProperties ()) {
				if (!pinfo.IsDefined (typeof (Gtk.ChildPropertyAttribute), true))
					continue;
				props[pinfo] = pinfo.GetValue (cc, null);
			}

			container.Remove (oldChild);
			AutoSize[oldChild] = false;
			AutoSize[newChild] = true;
			container.Add (newChild);

			cc = container[newChild];
			foreach (PropertyInfo pinfo in props.Keys)
				pinfo.SetValue (cc, props[pinfo], null);

			Sync ();
		}

		Gtk.Widget selection;
		HandleWindow handles;

		public virtual void Select (Stetic.Wrapper.Widget wrapper)
		{
			if (wrapper == null)
				Select (null, false);
			else
				Select (wrapper.Wrapped, (wrapper.InternalChildId == null));
			stetic.Selection = wrapper;
		}

		public virtual void UnSelect (Stetic.Wrapper.Widget wrapper)
		{
			if (selection == wrapper.Wrapped)
				Select (null, false);
		}

		public virtual void Select (Placeholder ph)
		{
			Select (ph, false);
			stetic.Selection = null;
		}

		void Select (Gtk.Widget widget, bool dragHandles)
		{
			if (widget == selection)
				return;
			selection = widget;

			if (handles != null)
				handles.Dispose ();

			if (selection != null) {
				handles = new HandleWindow (selection, dragHandles);
				handles.Drag += HandleWindowDrag;
			} else 
				handles = null;
		}

		Gtk.Widget dragSource;

		void HandleWindowDrag (Gdk.EventMotion evt)
		{
			Gtk.Widget dragWidget = selection;
			Gdk.Rectangle alloc = dragWidget.Allocation;

			Select ((Stetic.Wrapper.Widget)null);

			dragSource = CreatePlaceholder ();
			dragSource.SetSizeRequest (alloc.Width, alloc.Height);
			ReplaceChild (dragWidget, dragSource);
			DND.Drag (dragSource, evt, dragWidget);
		}

		void SizeAllocated (object obj, Gtk.SizeAllocatedArgs args)
		{
			if (handles != null)
				handles.Shape ();
		}

		public void Delete (Stetic.Wrapper.Widget wrapper)
		{
			if (wrapper.Wrapped == selection)
				Select (null, false);
			ReplaceChild (wrapper.Wrapped, CreatePlaceholder ());
			wrapper.Wrapped.Destroy ();
		}

		protected bool ChildHExpandable (Gtk.Widget child)
		{
			if (child == dragSource)
				child = DND.DragWidget;
			else if (child is Placeholder)
				return true;

			Stetic.Wrapper.Widget wrapper = Stetic.Wrapper.Widget.Lookup (child);
			if (wrapper != null)
				return wrapper.HExpandable;
			else
				return false;
		}

		protected bool ChildVExpandable (Gtk.Widget child)
		{
			if (child == dragSource)
				child = DND.DragWidget;
			else if (child is Placeholder)
				return true;

			Stetic.Wrapper.Widget wrapper = Stetic.Wrapper.Widget.Lookup (child);
			if (wrapper != null)
				return wrapper.VExpandable;
			else
				return false;
		}

		public class ContainerChild : Stetic.ObjectWrapper {

			public static new Type WrappedType = typeof (Gtk.Container.ContainerChild);

			static void Register ()
			{
				// FIXME?
			}

			public override void Wrap (object obj, bool initialized)
			{
				base.Wrap (obj, initialized);
				cc.Child.ChildNotified += ChildNotifyHandler;

				// FIXME; arrange for wrapper disposal?
			}

			public override void Dispose ()
			{
				cc.Child.ChildNotified -= ChildNotifyHandler;
				base.Dispose ();
			}

			protected virtual void ChildNotifyHandler (object obj, Gtk.ChildNotifiedArgs args)
			{
				ParamSpec pspec = new ParamSpec (args.Pspec);
				EmitNotify (pspec.Name);
			}

			protected override void EmitNotify (string propertyName)
			{
				base.EmitNotify (propertyName);
				ParentWrapper.Sync ();
			}

			Gtk.Container.ContainerChild cc {
				get {
					return (Gtk.Container.ContainerChild)Wrapped;
				}
			}

			protected Stetic.Wrapper.Container ParentWrapper {
				get {
					return Stetic.Wrapper.Container.Lookup (cc.Parent);
				}
			}

			[Description ("Auto Size", "If set, the other packing properties for this cell will be automatically adjusted as other widgets are added to and removed from the container")]
			public bool AutoSize {
				get {
					return ParentWrapper.AutoSize[cc.Child];
				}
				set {
					ParentWrapper.AutoSize[cc.Child] = value;
					EmitNotify ("AutoSize");
				}
			}
		}
	}
}
