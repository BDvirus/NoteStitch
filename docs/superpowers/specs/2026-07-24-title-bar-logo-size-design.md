# Title-Bar Logo Size Design

## Goal

Increase the NoteStitch logo in the custom title bar from 24×24 pixels to
40×40 pixels.

## Design

The existing title bar is 68 pixels high and already contains a 40×40
`AppIconBadge`. Only the nested `Image` dimensions will change. Its width and
height will both become 40 pixels, filling the existing container while
retaining centered alignment.

The title-bar height, padding, badge dimensions, text margin, typography,
caption buttons, drag region, source image, About page image, application icon,
and installer icon will remain unchanged.

## Testing

A static layout regression check will read `MainWindow.xaml`, locate the image
whose source is `ms-appx:///Assets/notes.png` inside `AppIconBadge`, and require
both dimensions to equal 40.

The application will then be built to validate the XAML.

## Scope

This change affects only the logo rendered in the main window title bar. It
does not edit or regenerate image assets.
