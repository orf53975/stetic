using Gtk;
using System;

namespace Stetic.Editor {

	[PropertyEditor ("Value", "Changed")]
	public class ThemedIcon : Gtk.HBox {
		Gtk.Image image;
		Gtk.Entry entry;
		Gtk.Button button;

		public ThemedIcon () : base (false, 6)
		{
			image = new Gtk.Image (Gnome.Stock.Blank, Gtk.IconSize.Button);
			PackStart (image, false, false, 0);

			entry = new Gtk.Entry ();
			PackStart (entry, true, true, 0);
			entry.Changed += entry_Changed;

			button = new Gtk.Button ();
			Gtk.Image icon = Gtk.Image.NewFromIconName ("stock_symbol-selection", Gtk.IconSize.Button);
			button.Add (icon);
			PackStart (button, false, false, 0);
			button.Clicked += button_Clicked;
		}

		public event EventHandler Changed;

		bool syncing;

		void entry_Changed (object obj, EventArgs args)
		{
			if (!syncing)
				Value = entry.Text;
		}

		void button_Clicked (object obj, EventArgs args)
		{
			Gtk.Window parent = (Gtk.Window)GetAncestor (Gtk.Window.GType);
			Value = ThemedIconBrowser.Browse (parent, Value);
		}

		string icon;
		public string Value {
			get {
				return icon; 
			}
			set {
				if (icon == value)
					return;

				icon = value;
				image.SetFromIconName (icon, Gtk.IconSize.Button);

				syncing = true;
				entry.Text = icon;
				syncing = false;

				if (Changed != null)
					Changed (this, EventArgs.Empty);
			}
		}
	}

	public class ThemedIconBrowser : Gtk.Dialog {

		public ThemedIconBrowser (Gtk.Window parent) :
			base ("Select a Themed Icon", parent, Gtk.DialogFlags.Modal,
			      Gtk.Stock.Cancel, Gtk.ResponseType.Cancel,
			      Gtk.Stock.Ok, Gtk.ResponseType.Ok)
		{
			HasSeparator = false;
			BorderWidth = 12;
			VBox.Spacing = 18;
			VBox.BorderWidth = 0;

			DefaultResponse = Gtk.ResponseType.Ok;

			Gtk.HBox hbox = new Gtk.HBox (false, 12);
			VBox.PackStart (hbox, false, false, 0);

			entry = new Gtk.Entry ();
			entry.Activated += DoFind;
			hbox.PackStart (entry);

			Gtk.Button button = new Gtk.Button (Gtk.Stock.Find);
			button.Clicked += DoFind;
			hbox.PackStart (button, false, false, 0);

			ScrolledWindow scwin = new Gtk.ScrolledWindow ();
			scwin.SizeRequested += ScrolledWindowSizeRequested;
			VBox.PackStart (scwin, true, true, 0);
			scwin.SetPolicy (Gtk.PolicyType.Never, Gtk.PolicyType.Automatic);
			scwin.ShadowType = Gtk.ShadowType.In;

			list = new ThemedIconList ();
			scwin.Add (list);
			list.SelectionChanged += ListSelectionChanged;
			list.Activated += ListActivated;
			SetResponseSensitive (Gtk.ResponseType.Ok, false);

			VBox.ShowAll ();
		}

		public static string Browse (Gtk.Window parent, string selection)
		{
			ThemedIconBrowser browser = new ThemedIconBrowser (parent);
			browser.list.Selection = Array.IndexOf (IconNames, selection);
			int response = browser.Run ();
			if (response == (int)Gtk.ResponseType.Ok)
				selection = IconNames[browser.list.Selection];
			browser.Destroy ();
			return selection;
		}

		Gtk.Entry entry;
		ThemedIconList list;

		void ScrolledWindowSizeRequested (object obj, SizeRequestedArgs args)
		{
			Gtk.Requisition req = list.SizeRequest ();
			if (req.Width <= 0)
				return;

			Gtk.ScrolledWindow scwin = ((Gtk.ScrolledWindow)obj);
			scwin.SizeRequested -= ScrolledWindowSizeRequested;
			scwin.SetSizeRequest (req.Width, req.Width * 2 / 3);
			ActionArea.BorderWidth = 0; // has to happen post-realize
		}

		void ListSelectionChanged (object obj, EventArgs args)
		{
			SetResponseSensitive (Gtk.ResponseType.Ok, list.Selection != -1);
		}

		void ListActivated (object obj, EventArgs args)
		{
			Respond (Gtk.ResponseType.Ok);
		}

		void DoFind (object obj, EventArgs args)
		{
			list.Find (entry.Text);
		}

		// This is the internal class the represents the actual two-column
		// icon list. This can't just be handled as an HBox inside the main
		// window, because we need to override SetScrollAdjustments.
		class ThemedIconList : Gtk.HBox {

			ThemedIconColumn left, right;

			public ThemedIconList () : base (true, 0)
			{
				left = new ThemedIconColumn ();
				PackStart (left);
				right = new ThemedIconColumn ();
				PackStart (right);

				left.Selection.Changed += LeftSelectionChanged;
				right.Selection.Changed += RightSelectionChanged;
				left.RowActivated += RowActivated;
				right.RowActivated += RowActivated;
				left.KeyPressEvent += ColumnKeyPressEvent;
				right.KeyPressEvent += ColumnKeyPressEvent;

				Gtk.IconTheme theme = Gtk.IconTheme.Default;
				for (int i = 0; i < IconNames.Length; i++) {
					if (i % 2 == 0)
						left.Append (theme, IconNames[i]);
					else
						right.Append (theme, IconNames[i]);
				}
			}

			public event EventHandler Activated;

			void RowActivated (object obj, RowActivatedArgs args)
			{
				if (Activated != null)
					Activated (this, EventArgs.Empty);
			}

			public int Selection {
				get {
					Gtk.TreePath[] selection;
					selection = left.Selection.GetSelectedRows ();
					if (selection.Length > 0)
						return selection[0].Indices[0] * 2;
					selection = right.Selection.GetSelectedRows ();
					if (selection.Length > 0)
						return selection[0].Indices[0] * 2 + 1;
					return -1;
				}
				set {
					if (value == -1) {
						left.Selection.UnselectAll ();
						right.Selection.UnselectAll ();
						return;
					}
					if (value % 2 == 0)
						left.SelectRow (value / 2);
					else
						right.SelectRow (value / 2);
				}
			}

			public event EventHandler SelectionChanged;

			public void Find (string text)
			{
				int selection = Selection;
				for (int i = (selection + 1) % IconNames.Length; i != selection; i = (i + 1) % IconNames.Length) {
					if (IconNames[i].IndexOf (text) != -1) {
						Selection = i;
						return;
					}
				}
				Selection = -1;
			}

			void LeftSelectionChanged (object obj, EventArgs args)
			{
				if (left.Selection.GetSelectedRows().Length != 0)
					right.Selection.UnselectAll ();
				if (SelectionChanged != null)
					SelectionChanged (this, EventArgs.Empty);
			}

			void RightSelectionChanged (object obj, EventArgs args)
			{
				if (right.Selection.GetSelectedRows().Length != 0)
					left.Selection.UnselectAll ();
				if (SelectionChanged != null)
					SelectionChanged (this, EventArgs.Empty);
			}

			[GLib.ConnectBefore]
			void ColumnKeyPressEvent (object obj, KeyPressEventArgs args)
			{
				if (args.Event.Key == Gdk.Key.Right) {
					if (obj == (object)left) {
						Selection++;
						right.GrabFocus ();
					}
					args.RetVal = true;
				} else if (args.Event.Key == Gdk.Key.Left) {
					if (obj == (object)right) {
						Selection--;
						left.GrabFocus ();
					}
					args.RetVal = true;
				}
			}

			protected override void OnSetScrollAdjustments (Gtk.Adjustment hadj, Gtk.Adjustment vadj)
			{
				left.SetScrollAdjustments (null, vadj);
				right.SetScrollAdjustments (null, vadj);
			}
		}

		// Another internal class. This is a single column of the ThemedIconList
		class ThemedIconColumn : Gtk.TreeView {
			public ThemedIconColumn ()
			{
				Model = store = new Gtk.ListStore (typeof (Gdk.Pixbuf),
								   typeof (string));
				HeadersVisible = false;
				EnableSearch = false;

				TreeViewColumn col;
				CellRenderer renderer;

				col = new TreeViewColumn ();
				renderer = new CellRendererPixbuf ();
				col.PackStart (renderer, false);
				col.AddAttribute (renderer, "pixbuf", 0);
				renderer = new CellRendererText ();
				col.PackStart (renderer, false);
				col.AddAttribute (renderer, "text", 1);
				AppendColumn (col);
			}

			Gtk.ListStore store;

			public void Append (Gtk.IconTheme theme, string name)
			{
				Gdk.Pixbuf pixbuf;
				try {
					pixbuf = theme.LoadIcon (name, 16, 0);
				} catch {
					pixbuf = RenderIcon (name, Gtk.IconSize.Menu, null);
				}
				store.AppendValues (pixbuf, name);
			}

			public void SelectRow (int row)
			{
				Gtk.TreeIter iter;
				if (store.IterNthChild (out iter, row)) {
					Gtk.TreePath path = store.GetPath (iter);

					SetCursor (path, null, false);

					// We want the initial selection to be centered
					if (!IsRealized)
						ScrollToCell (path, null, true, 0.5f, 0.0f);
				}
			}
		}
			
		static string[] IconNames = new string[] {
			// Gtk 2.6 stock icons
			"gtk-about",
			"gtk-add",
			"gtk-apply",
			"gtk-bold",
			"gtk-cancel",
			"gtk-cdrom",
			"gtk-clear",
			"gtk-close",
			"gtk-color-picker",
			"gtk-connect",
			"gtk-convert",
			"gtk-copy",
			"gtk-cut",
			"gtk-delete",
			"gtk-dialog-authentication",
			"gtk-dialog-error",
			"gtk-dialog-info",
			"gtk-dialog-question",
			"gtk-dialog-warning",
			"gtk-directory",
			"gtk-disconnect",
			"gtk-dnd",
			"gtk-dnd-multiple",
			"gtk-edit",
			"gtk-execute",
			"gtk-file",
			"gtk-find",
			"gtk-find-and-replace",
			"gtk-floppy",
			"gtk-go-back",
			"gtk-go-down",
			"gtk-go-forward",
			"gtk-go-up",
			"gtk-goto-bottom",
			"gtk-goto-first",
			"gtk-goto-last",
			"gtk-goto-top",
			"gtk-harddisk",
			"gtk-help",
			"gtk-home",
			"gtk-indent",
			"gtk-index",
			"gtk-italic",
			"gtk-jump-to",
			"gtk-justify-center",
			"gtk-justify-fill",
			"gtk-justify-left",
			"gtk-justify-right",
			"gtk-media-forward",
			"gtk-media-next",
			"gtk-media-pause",
			"gtk-media-play",
			"gtk-media-previous",
			"gtk-media-record",
			"gtk-media-rewind",
			"gtk-media-stop",
			"gtk-missing-image",
			"gtk-network",
			"gtk-new",
			"gtk-no",
			"gtk-ok",
			"gtk-open",
			"gtk-paste",
			"gtk-preferences",
			"gtk-print",
			"gtk-print-preview",
			"gtk-properties",
			"gtk-quit",
			"gtk-redo",
			"gtk-refresh",
			"gtk-remove",
			"gtk-revert-to-saved",
			"gtk-save",
			"gtk-save-as",
			"gtk-select-color",
			"gtk-select-font",
			"gtk-sort-ascending",
			"gtk-sort-descending",
			"gtk-spell-check",
			"gtk-stop",
			"gtk-strikethrough",
			"gtk-undelete",
			"gtk-underline",
			"gtk-undo",
			"gtk-unindent",
			"gtk-yes",
			"gtk-zoom-100",
			"gtk-zoom-fit",
			"gtk-zoom-in",
			"gtk-zoom-out",

			// Themable stock icons
			"stock_about",
			"stock_active",
			"stock_add-bookmark",
			"stock_add-decimal-place",
			"stock_addressbook",
			"stock_advanced-filter",
			"stock_alarm",
			"stock_alignment",
			"stock_alignment-bottom",
			"stock_alignment-centered",
			"stock_alignment-centered-vertically",
			"stock_alignment-left",
			"stock_alignment-right",
			"stock_alignment-top",
			"stock_allow-effects",
			"stock_anchor",
			"stock_animation",
			"stock_appointment-reminder",
			"stock_appointment-reminder-excl",
			"stock_arrowstyle",
			"stock_attach",
			"stock_auto-contour",
			"stock_autocompletion",
			"stock_autofilter",
			"stock_autoformat",
			"stock_autopilot",
			"stock_autopilot-24",
			"stock_autospellcheck",
			"stock_autotext",
			"stock_bell",
			"stock_bluetooth",
			"stock_book_blue",
			"stock_book_green",
			"stock_book_open",
			"stock_book_red",
			"stock_book_yellow",
			"stock_bookmark",
			"stock_bottom",
			"stock_briefcase",
			"stock_brightness",
			"stock_bring-backward",
			"stock_bring-forward",
			"stock_bucketfill",
			"stock_calc-accept",
			"stock_calc-cancel",
			"stock_calendar",
			"stock_calendar-and-tasks",
			"stock_calendar-view-day",
			"stock_calendar-view-list",
			"stock_calendar-view-month",
			"stock_calendar-view-week",
			"stock_calendar-view-work-week",
			"stock_calendar-view-year",
			"stock_cell-align-bottom",
			"stock_cell-align-center",
			"stock_cell-align-top",
			"stock_cell-phone",
			"stock_certificate",
			"stock_channel",
			"stock_channel-blue",
			"stock_channel-green",
			"stock_channel-red",
			"stock_chart",
			"stock_chart-autoformat",
			"stock_chart-data-in-columns",
			"stock_chart-data-in-rows",
			"stock_chart-edit-type",
			"stock_chart-reorganize",
			"stock_chart-scale-text",
			"stock_chart-toggle-axes",
			"stock_chart-toggle-axes-title",
			"stock_chart-toggle-hgrid",
			"stock_chart-toggle-legend",
			"stock_chart-toggle-title",
			"stock_chart-toggle-vgrid",
			"stock_check-filled",
			"stock_choose-themes",
			"stock_close",
			"stock_color",
			"stock_compile",
			"stock_connect",
			"stock_connect-to-url",
			"stock_contact",
			"stock_contact-list",
			"stock_contrast",
			"stock_copy",
			"stock_create-with-attributes",
			"stock_creditcard",
			"stock_crop",
			"stock_cut",
			"stock_data-delete-link",
			"stock_data-delete-query",
			"stock_data-delete-record",
			"stock_data-delete-sql-query",
			"stock_data-delete-table",
			"stock_data-edit-link",
			"stock_data-edit-query",
			"stock_data-edit-sql-query",
			"stock_data-edit-table",
			"stock_data-explorer",
			"stock_data-first",
			"stock_data-last",
			"stock_data-link",
			"stock_data-linked-table",
			"stock_data-links",
			"stock_data-new-link",
			"stock_data-new-query",
			"stock_data-new-record",
			"stock_data-new-sql-query",
			"stock_data-new-table",
			"stock_data-next",
			"stock_data-previous",
			"stock_data-queries",
			"stock_data-query",
			"stock_data-query-rename",
			"stock_data-save",
			"stock_data-sources",
			"stock_data-sources-delete",
			"stock_data-sources-hand",
			"stock_data-sources-modified",
			"stock_data-sources-new",
			"stock_data-table",
			"stock_data-tables",
			"stock_data-undo",
			"stock_datapilot",
			"stock_decrease-font",
			"stock_default-folder",
			"stock_delete",
			"stock_delete-autofilter",
			"stock_delete-bookmark",
			"stock_delete-column",
			"stock_delete-decimal-place",
			"stock_delete-row",
			"stock_dialog-error",
			"stock_dialog-info",
			"stock_dialog-question",
			"stock_dialog-warning",
			"stock_directcursor",
			"stock_directory-server",
			"stock_disconnect",
			"stock_display-grid",
			"stock_display-guides",
			"stock_distort",
			"stock_down",
			"stock_down-with-subpoints",
			"stock_drag-mode",
			"stock_draw-arc",
			"stock_draw-callouts",
			"stock_draw-circle",
			"stock_draw-circle-arc",
			"stock_draw-circle-pie",
			"stock_draw-circle-pie-unfilled",
			"stock_draw-circle-segment",
			"stock_draw-circle-segment-unfilled",
			"stock_draw-circle-unfilled",
			"stock_draw-cone",
			"stock_draw-connector",
			"stock_draw-connector-ends-with-arrow",
			"stock_draw-connector-ends-with-circle",
			"stock_draw-connector-starts-with-arrow",
			"stock_draw-connector-starts-with-circle",
			"stock_draw-connector-with-arrows",
			"stock_draw-connector-with-circles",
			"stock_draw-cube",
			"stock_draw-curve",
			"stock_draw-curve-filled",
			"stock_draw-curved-connector",
			"stock_draw-curved-connector-ends-with-arrow",
			"stock_draw-curved-connector-ends-with-circle",
			"stock_draw-curved-connector-starts-with-arrow",
			"stock_draw-curved-connector-starts-with-circle",
			"stock_draw-curved-connector-with-arrows",
			"stock_draw-curved-connector-with-circles",
			"stock_draw-cylinder",
			"stock_draw-dimension-line",
			"stock_draw-ellipse",
			"stock_draw-ellipse-pie",
			"stock_draw-ellipse-pie-unfilled",
			"stock_draw-ellipse-segment",
			"stock_draw-ellipse-segment-unfilled",
			"stock_draw-ellipse-unfilled",
			"stock_draw-freeform-line",
			"stock_draw-freeform-line-filled",
			"stock_draw-half-sphere",
			"stock_draw-line",
			"stock_draw-line-45",
			"stock_draw-line-connector",
			"stock_draw-line-connector-ends-with-arrow",
			"stock_draw-line-connector-ends-with-circle",
			"stock_draw-line-connector-starts-with-arrow",
			"stock_draw-line-connector-starts-with-circle",
			"stock_draw-line-connector-with-arrows",
			"stock_draw-line-connector-with-circles",
			"stock_draw-line-ends-with-arrow",
			"stock_draw-line-starts-with-arrow",
			"stock_draw-line-with-arrow-circle",
			"stock_draw-line-with-arrow-square",
			"stock_draw-line-with-arrows",
			"stock_draw-line-with-circle-arrow",
			"stock_draw-line-with-square-arrow",
			"stock_draw-polygon",
			"stock_draw-polygon-45",
			"stock_draw-polygon-45-filled",
			"stock_draw-polygon-filled",
			"stock_draw-pyramid",
			"stock_draw-rectangle",
			"stock_draw-rectangle-unfilled",
			"stock_draw-rounded-rectangle",
			"stock_draw-rounded-rectangle-unfilled",
			"stock_draw-rounded-square",
			"stock_draw-rounded-square-unfilled",
			"stock_draw-selection",
			"stock_draw-shell",
			"stock_draw-sphere",
			"stock_draw-square",
			"stock_draw-square-unfilled",
			"stock_draw-straight-connector",
			"stock_draw-straight-connector-ends-with-arrow",
			"stock_draw-straight-connector-ends-with-circle",
			"stock_draw-straight-connector-starts-with-arrow",
			"stock_draw-straight-connector-starts-with-circle",
			"stock_draw-straight-connector-with-arrows",
			"stock_draw-straight-connector-with-circles",
			"stock_draw-text",
			"stock_draw-text-animation",
			"stock_draw-text-frame",
			"stock_draw-torus",
			"stock_draw-vertical-callouts",
			"stock_draw-vertical-text",
			"stock_draw-vertical-text-frame",
			"stock_edit",
			"stock_edit-bookmark",
			"stock_edit-contour",
			"stock_edit-headers-and-footers",
			"stock_edit-points",
			"stock_effects",
			"stock_effects-more-options",
			"stock_effects-object",
			"stock_effects-object-colorize",
			"stock_effects-object-hide",
			"stock_effects-play-in-full",
			"stock_effects-preview",
			"stock_effects-sound",
			"stock_effects-text",
			"stock_enter-group",
			"stock_equals",
			"stock_error-next",
			"stock_error-next-16",
			"stock_error-previous",
			"stock_error-previous-16",
			"stock_euro",
			"stock_example",
			"stock_exchange-columns",
			"stock_exchange-connector",
			"stock_exchange-rows",
			"stock_exit",
			"stock_exit-group",
			"stock_export",
			"stock_extended-help",
			"stock_file-properties",
			"stock_file-with-objects",
			"stock_filter-data-by-criteria",
			"stock_filter-navigator",
			"stock_filters",
			"stock_filters-aging",
			"stock_filters-charcoal",
			"stock_filters-invert",
			"stock_filters-pixelize",
			"stock_filters-pop-art",
			"stock_filters-posterize",
			"stock_filters-relief",
			"stock_filters-remove-noise",
			"stock_filters-sharpen",
			"stock_filters-smooth",
			"stock_filters-solarize",
			"stock_first",
			"stock_first-page",
			"stock_flip",
			"stock_flip-horizontally",
			"stock_flip-vertically",
			"stock_folder",
			"stock_folder-copy",
			"stock_folder-move",
			"stock_folder-properties",
			"stock_font",
			"stock_font-formatting-toggle",
			"stock_font-size",
			"stock_fontwork",
			"stock_fontwork-2dshadow",
			"stock_fontwork-3dshadow",
			"stock_fontwork-adaptation-off",
			"stock_fontwork-adaptation-rotate",
			"stock_fontwork-adaptation-slant-h",
			"stock_fontwork-adaptation-slant-v",
			"stock_fontwork-adaptation-straight",
			"stock_fontwork-align-fill",
			"stock_fontwork-noshadow",
			"stock_fontwork-preview-spline",
			"stock_fontwork-reverse-text-flow",
			"stock_fontwork-shadow-angle",
			"stock_fontwork-shadow-length",
			"stock_fontwork-shadow-x-offset",
			"stock_fontwork-shadow-y-offset",
			"stock_fontwork-spline-distance",
			"stock_fontwork-spline-indent",
			"stock_fontwork-text-border",
			"stock_form-activation-order",
			"stock_form-add-field",
			"stock_form-automatic-control-focus",
			"stock_form-autopilots",
			"stock_form-button",
			"stock_form-checkbox",
			"stock_form-combobox",
			"stock_form-control-properties",
			"stock_form-currency-field",
			"stock_form-date-field",
			"stock_form-design-mode",
			"stock_form-file-selection",
			"stock_form-formatted-field",
			"stock_form-frame",
			"stock_form-image-button",
			"stock_form-image-control",
			"stock_form-label",
			"stock_form-letter-dialog",
			"stock_form-line-horizontal",
			"stock_form-line-vertical",
			"stock_form-listbox",
			"stock_form-navigator",
			"stock_form-numerical-field",
			"stock_form-open-in-design-mode",
			"stock_form-pattern-field",
			"stock_form-progressbar",
			"stock_form-properties",
			"stock_form-radio",
			"stock_form-table-control",
			"stock_form-text-box",
			"stock_form-time-field",
			"stock_format-character",
			"stock_format-default",
			"stock_format-numbering-bullets",
			"stock_format-object",
			"stock_format-page",
			"stock_format-paragraph",
			"stock_format-percent",
			"stock_format-scientific",
			"stock_formula-cursor",
			"stock_frame",
			"stock_fullscreen",
			"stock_function-autopilot",
			"stock_gamma",
			"stock_glue",
			"stock_gluepoint-down",
			"stock_gluepoint-horizontal-center",
			"stock_gluepoint-horizontal-left",
			"stock_gluepoint-horizontal-right",
			"stock_gluepoint-left",
			"stock_gluepoint-relative",
			"stock_gluepoint-right",
			"stock_gluepoint-up",
			"stock_gluepoint-vertical-bottom",
			"stock_gluepoint-vertical-center",
			"stock_gluepoint-vertical-top",
			"stock_goal-seek",
			"stock_gradient",
			"stock_graphic-styles",
			"stock_graphics-align-bottom",
			"stock_graphics-align-center",
			"stock_graphics-align-centered",
			"stock_graphics-align-left",
			"stock_graphics-align-right",
			"stock_graphics-align-top",
			"stock_group",
			"stock_group-cells",
			"stock_groupwise-connector",
			"stock_guides",
			"stock_hand-signed",
			"stock_handles-big",
			"stock_handles-simple",
			"stock_headphones",
			"stock_help",
			"stock_help-add-bookmark",
			"stock_help-agent",
			"stock_help-book",
			"stock_help-book-open",
			"stock_help-chat",
			"stock_help-document",
			"stock_help-pane-off",
			"stock_help-pane-on",
			"stock_home",
			"stock_hyperlink",
			"stock_hyperlink-internet-search",
			"stock_hyperlink-target",
			"stock_hyperlink-toolbar",
			"stock_id",
			"stock_imagemap-editor",
			"stock_inbox",
			"stock_increase-font",
			"stock_init",
			"stock_insert-applet",
			"stock_insert-caption",
			"stock_insert-cells",
			"stock_insert-cells-down",
			"stock_insert-cells-right",
			"stock_insert-chart",
			"stock_insert-columns",
			"stock_insert-cross-reference",
			"stock_insert-fields",
			"stock_insert-fields-author",
			"stock_insert-fields-subject",
			"stock_insert-fields-title",
			"stock_insert-file",
			"stock_insert-floating-frame",
			"stock_insert-footer",
			"stock_insert-form",
			"stock_insert-gluepoint",
			"stock_insert-header",
			"stock_insert-math-object",
			"stock_insert-names-define",
			"stock_insert-note",
			"stock_insert-ole-object",
			"stock_insert-plugin",
			"stock_insert-rows",
			"stock_insert-rule",
			"stock_insert-single-column-text-frame",
			"stock_insert-slide",
			"stock_insert-sound-plugin",
			"stock_insert-table",
			"stock_insert-text-frame",
			"stock_insert-url",
			"stock_insert-video-plugin",
			"stock_insert_endnote",
			"stock_insert_footnote",
			"stock_insert_graphic",
			"stock_insert_image",
			"stock_insert_index_marker",
			"stock_insert_section",
			"stock_insert_special_character",
			"stock_interaction",
			"stock_internet",
			"stock_keyring",
			"stock_landline-phone",
			"stock_last",
			"stock_last-page",
			"stock_left",
			"stock_left-with-subpoints",
			"stock_line-spacing-1",
			"stock_line-spacing-1.5",
			"stock_line-spacing-2",
			"stock_line_in",
			"stock_linepen",
			"stock_link",
			"stock_list-insert-unnumbered",
			"stock_list_bullet",
			"stock_list_enum",
			"stock_list_enum-off",
			"stock_list_enum-restart",
			"stock_live-mode",
			"stock_lock",
			"stock_lock-broken",
			"stock_lock-ok",
			"stock_lock-open",
			"stock_macro-check-brackets",
			"stock_macro-controls",
			"stock_macro-insert",
			"stock_macro-insert-breakpoint",
			"stock_macro-jump-back",
			"stock_macro-objects",
			"stock_macro-organizer",
			"stock_macro-stop-after-command",
			"stock_macro-stop-after-procedure",
			"stock_macro-stop-watching",
			"stock_macro-watch-variable",
			"stock_mail",
			"stock_mail-accounts",
			"stock_mail-compose",
			"stock_mail-copy",
			"stock_mail-druid",
			"stock_mail-druid-account",
			"stock_mail-filters-apply",
			"stock_mail-flag-for-followup",
			"stock_mail-flag-for-followup-done",
			"stock_mail-forward",
			"stock_mail-handling",
			"stock_mail-hide-deleted",
			"stock_mail-hide-read",
			"stock_mail-hide-selected",
			"stock_mail-import",
			"stock_mail-merge",
			"stock_mail-move",
			"stock_mail-open",
			"stock_mail-open-multiple",
			"stock_mail-priority-high",
			"stock_mail-receive",
			"stock_mail-replied",
			"stock_mail-reply",
			"stock_mail-reply-to-all",
			"stock_mail-send",
			"stock_mail-send-receive",
			"stock_mail-unread",
			"stock_mail-unread-multiple",
			"stock_mark",
			"stock_media-fwd",
			"stock_media-next",
			"stock_media-pause",
			"stock_media-play",
			"stock_media-prev",
			"stock_media-rec",
			"stock_media-rew",
			"stock_media-shuffle",
			"stock_media-stop",
			"stock_message-display",
			"stock_mic",
			"stock_midi",
			"stock_modify-layout",
			"stock_music-library",
			"stock_my-documents",
			"stock_navigate-next",
			"stock_navigate-prev",
			"stock_navigator",
			"stock_navigator-all-or-sel-toggle",
			"stock_navigator-database-ranges",
			"stock_navigator-drag-mode",
			"stock_navigator-edit-entry",
			"stock_navigator-foonote-body-toggle",
			"stock_navigator-footer-body-toggle",
			"stock_navigator-header-body-toggle",
			"stock_navigator-headings",
			"stock_navigator-indexes",
			"stock_navigator-insert-as-copy",
			"stock_navigator-insert-as-link",
			"stock_navigator-insert-index",
			"stock_navigator-insert-linked",
			"stock_navigator-levels",
			"stock_navigator-list-box-toggle",
			"stock_navigator-master-toggle",
			"stock_navigator-next-object",
			"stock_navigator-open-toolbar",
			"stock_navigator-previous-object",
			"stock_navigator-range-names",
			"stock_navigator-references",
			"stock_navigator-reminder",
			"stock_navigator-scenarios",
			"stock_navigator-sections",
			"stock_navigator-shift-down",
			"stock_navigator-shift-left",
			"stock_navigator-shift-right",
			"stock_navigator-shift-up",
			"stock_navigator-table-formula",
			"stock_navigator-text",
			"stock_navigator-update-entry",
			"stock_navigator-wrong-table-formula",
			"stock_network-printer",
			"stock_new",
			"stock_new",
			"stock_new-24h-appointment",
			"stock_new-appointment",
			"stock_new-bcard",
			"stock_new-dir",
			"stock_new-drawing",
			"stock_new-formula",
			"stock_new-html",
			"stock_new-labels",
			"stock_new-master-document",
			"stock_new-meeting",
			"stock_new-presentation",
			"stock_new-spreadsheet",
			"stock_new-tab",
			"stock_new-template",
			"stock_new-text",
			"stock_new-window",
			"stock_news",
			"stock_next",
			"stock_next-page",
			"stock_node-add",
			"stock_node-close-path",
			"stock_node-convert",
			"stock_node-corner",
			"stock_node-corner-to-smooth",
			"stock_node-curve-split",
			"stock_node-delete",
			"stock_node-mark-for-deletion",
			"stock_node-move",
			"stock_node-smooth-to-symmetrical",
			"stock_nonprinting-chars",
			"stock_not",
			"stock_not-spam",
			"stock_notebook",
			"stock_notes",
			"stock_object-behind",
			"stock_object-infront",
			"stock_online-layout",
			"stock_open",
			"stock_open-read-only",
			"stock_openoffice",
			"stock_opensave",
			"stock_outbox",
			"stock_page-number",
			"stock_page-total-number",
			"stock_paragraph-spacing-decrease",
			"stock_paragraph-spacing-increase",
			"stock_paste",
			"stock_people",
			"stock_person",
			"stock_pin",
			"stock_placeholder-graphic",
			"stock_placeholder-line-contour",
			"stock_placeholder-picture",
			"stock_placeholder-text",
			"stock_playlist",
			"stock_position-size",
			"stock_post-message",
			"stock_presentation-box",
			"stock_presentation-styles",
			"stock_preview-four-pages",
			"stock_preview-two-pages",
			"stock_previous",
			"stock_previous-page",
			"stock_print",
			"stock_print-driver",
			"stock_print-duplex",
			"stock_print-duplex-no-tumble",
			"stock_print-duplex-tumble",
			"stock_print-layout",
			"stock_print-non-duplex",
			"stock_print-options",
			"stock_print-preview",
			"stock_print-preview-print",
			"stock_print-resolution",
			"stock_print-setup",
			"stock_printers",
			"stock_properties",
			"stock_proxy",
			"stock_quickmask",
			"stock_record-macro",
			"stock_record-number",
			"stock_redo",
			"stock_refresh",
			"stock_reload",
			"stock_repeat",
			"stock_reverse-order",
			"stock_right",
			"stock_right-with-subpoints",
			"stock_rotate",
			"stock_rotate-3d",
			"stock_rotation-mode",
			"stock_run-macro",
			"stock_samples",
			"stock_save",
			"stock_save-as",
			"stock_save-pdf",
			"stock_save-template",
			"stock_save_as",
			"stock_score-high",
			"stock_score-higher",
			"stock_score-highest",
			"stock_score-low",
			"stock_score-lower",
			"stock_score-lowest",
			"stock_score-normal",
			"stock_scores",
			"stock_script",
			"stock_script",
			"stock_scripts",
			"stock_search",
			"stock_search-and-replace",
			"stock_select-all",
			"stock_select-cell",
			"stock_select-column",
			"stock_select-row",
			"stock_select-table",
			"stock_send-fax",
			"stock_sent-mail",
			"stock_shadow",
			"stock_show-all",
			"stock_show-draw-functions",
			"stock_show-form-dialog",
			"stock_show-hidden-controls",
			"stock_shuffle",
			"stock_signature",
			"stock_signature-bad",
			"stock_signature-ok",
			"stock_slide-design",
			"stock_slide-duplicate",
			"stock_slide-expand",
			"stock_slide-reherse-timings",
			"stock_slide-show",
			"stock_slide-showhide",
			"stock_smart-playlist",
			"stock_smiley-1",
			"stock_smiley-2",
			"stock_smiley-3",
			"stock_smiley-4",
			"stock_smiley-5",
			"stock_smiley-6",
			"stock_smiley-7",
			"stock_smiley-8",
			"stock_smiley-9",
			"stock_smiley-10",
			"stock_smiley-11",
			"stock_smiley-12",
			"stock_smiley-13",
			"stock_smiley-14",
			"stock_smiley-15",
			"stock_smiley-16",
			"stock_smiley-17",
			"stock_smiley-18",
			"stock_smiley-19",
			"stock_smiley-20",
			"stock_smiley-21",
			"stock_smiley-22",
			"stock_smiley-23",
			"stock_smiley-24",
			"stock_smiley-25",
			"stock_smiley-26",
			"stock_snap-grid",
			"stock_snap-guides",
			"stock_snap-margins",
			"stock_snap-object",
			"stock_snap-object-points",
			"stock_sort-ascending",
			"stock_sort-column-ascending",
			"stock_sort-criteria",
			"stock_sort-descending",
			"stock_sort-row-ascending",
			"stock_sort-table-column-ascending",
			"stock_sort-table-row-ascending",
			"stock_sound",
			"stock_spam",
			"stock_spellcheck",
			"stock_standard-filter",
			"stock_stop",
			"stock_styles",
			"stock_styles-character-styles",
			"stock_styles-fill-format-mode",
			"stock_styles-frame-styles",
			"stock_styles-new-style-from-selection",
			"stock_styles-numbering-styles",
			"stock_styles-page-styles",
			"stock_styles-paragraph-styles",
			"stock_styles-update-style",
			"stock_subscript",
			"stock_sum",
			"stock_summary",
			"stock_superscript",
			"stock_symbol-selection",
			"stock_table-align-bottom",
			"stock_table-align-center",
			"stock_table-align-top",
			"stock_table-borders",
			"stock_table-combine",
			"stock_table-fit-height",
			"stock_table-fit-width",
			"stock_table-fixed",
			"stock_table-fixed-proportional",
			"stock_table-line-color",
			"stock_table-line-style",
			"stock_table-optimize",
			"stock_table-same-height",
			"stock_table-same-width",
			"stock_table-split",
			"stock_table-variable",
			"stock_table_borders",
			"stock_table_fill",
			"stock_task",
			"stock_task-assigned",
			"stock_task-assigned-to",
			"stock_task-recurring",
			"stock_test-mode",
			"stock_text-direction-ltr",
			"stock_text-direction-ttb",
			"stock_text-double-click-to-edit",
			"stock_text-monospaced",
			"stock_text-outline",
			"stock_text-quickedit",
			"stock_text-select-text-only",
			"stock_text-shadow",
			"stock_text-spacing",
			"stock_text-strikethrough",
			"stock_text_bold",
			"stock_text_center",
			"stock_text_color_background",
			"stock_text_color_foreground",
			"stock_text_color_hilight",
			"stock_text_indent",
			"stock_text_italic",
			"stock_text_justify",
			"stock_text_left",
			"stock_text_right",
			"stock_text_underlined",
			"stock_text_underlined-double",
			"stock_text_unindent",
			"stock_thesaurus",
			"stock_3d-3d-attributes-only",
			"stock_3d-all-attributes",
			"stock_3d-color-picker",
			"stock_3d-colors",
			"stock_3d-custom-color",
			"stock_3d-effects",
			"stock_3d-favourites",
			"stock_3d-geometry",
			"stock_3d-light",
			"stock_3d-light-off",
			"stock_3d-light-on",
			"stock_3d-material",
			"stock_3d-normals-double-sided",
			"stock_3d-normals-double-sided-closed-body",
			"stock_3d-normals-flat",
			"stock_3d-normals-flip-illumination",
			"stock_3d-normals-object-specific",
			"stock_3d-normals-spherical",
			"stock_3d-perspective",
			"stock_3d-shading",
			"stock_3d-texture",
			"stock_3d-texture-and-shading",
			"stock_3d-texture-object-specific",
			"stock_3d-texture-only",
			"stock_3d-texture-parallel",
			"stock_3d-texture-spherical",
			"stock_3dsound",
			"stock_timer",
			"stock_timer_stopped",
			"stock_timezone",
			"stock_to-3d",
			"stock_to-3d-rotation-object",
			"stock_to-background",
			"stock_to-bottom",
			"stock_to-curve",
			"stock_to-foreground",
			"stock_to-polygon",
			"stock_to-top",
			"stock_todo",
			"stock_toggle-graphics",
			"stock_toggle-info",
			"stock_toggle-preview",
			"stock_toilet-paper",
			"stock_tools-hyphenation",
			"stock_tools-macro",
			"stock_top",
			"stock_transform-circle-perspective",
			"stock_transform-circle-slant",
			"stock_transparency",
			"stock_trash_full",
			"stock_undelete",
			"stock_undo",
			"stock_undo-history",
			"stock_ungroup",
			"stock_ungroup-cells",
			"stock_unknown",
			"stock_unlink",
			"stock_up",
			"stock_up-one-dir",
			"stock_up-with-subpoints",
			"stock_update-data",
			"stock_update-fields",
			"stock_video-conferencing",
			"stock_view-details",
			"stock_view-field-shadings",
			"stock_view-fields",
			"stock_view-function-selection",
			"stock_view-html-source",
			"stock_volume",
			"stock_wallpaper-center",
			"stock_wallpaper-fill",
			"stock_wallpaper-scale",
			"stock_wallpaper-tile",
			"stock_weather-cloudy",
			"stock_weather-few-clouds",
			"stock_weather-fog",
			"stock_weather-night-clear",
			"stock_weather-night-few-clouds",
			"stock_weather-showers",
			"stock_weather-snow",
			"stock_weather-storm",
			"stock_weather-sunny",
			"stock_web-calendar",
			"stock_web-support",
			"stock_wrap-around",
			"stock_wrap-behind",
			"stock_wrap-contour",
			"stock_wrap-interrupt",
			"stock_wrap-left",
			"stock_wrap-optimal",
			"stock_wrap-right",
			"stock_zoom",
			"stock_zoom-1",
			"stock_zoom-in",
			"stock_zoom-next",
			"stock_zoom-object",
			"stock_zoom-optimal",
			"stock_zoom-out",
			"stock_zoom-page",
			"stock_zoom-page-width",
			"stock_zoom-previous",
			"stock_zoom-shift",
		};
	}
}
