# Codist
*Codist* is a visual studio extension which strives to provide better coding experience for C# programmers.
* advanced syntax highlighting
* C# comment tagger
* scrollbar marker for
  * special comment tags
  * C# type declarations
  * C# directives
  * line numbers
* increased margin between lines

# Features and screenshots

## Advanced C# syntax highlight
* The advanced syntax highlight function highlight every aspect of C# language elements with various styles and font settings, changing font style sizes, changing background and foreground colors, making text semitransparent.
  ![Syntax highlight](doc/syntax_highlight.png)

## Customized comment styles
* The comment tagger highlights comments (actually all editor styles) to your specific styles.
  ![Comment syntax highlight](doc/syntax_highlight2.png)
* The syntax style of C# XML Documentations could be changed too. You can make them semitrasparent to stand behind usual code lines.
  ![XML Comment syntax highlight](doc/xml_comment.png)

## Markers on the Scrollbar Margin

The scrollbar can mark...

* C# `class`/`struct`/`interface`/`enum` **declarations**
* C# instructions (`#if`, `#else`, `#region`, `#pragma`)
* **Line numbers**
  ![Scrollbar margin and line numbers](doc/scroll_margin.png)

# Customization
1. Open the *Codist* section in the *Tools->Options* dialog.
1. By default, *Codist* does not make many changes to your syntax.
1. Change the settings according to your preferences and click the OK button.
  ![Style customization](doc/customize.png)
1. It is also possible to increase line heights to make code text more readable.
  ![Style customization](doc/customize4.png)

# Acknowledgements
I have learned a lot from the following extension projects.
* Better comments: https://marketplace.visualstudio.com/items?itemName=OmarRwemi.BetterComments
* CommentsPlus: https://marketplace.visualstudio.com/items?itemName=mhoumann.CommentsPlus
* Match Margin: https://marketplace.visualstudio.com/items?itemName=VisualStudioProductTeam.MatchMargin
* Inheritance Margin: https://marketplace.visualstudio.com/items?itemName=SamHarwell.InheritanceMargin
* Font Sizer: https://marketplace.visualstudio.com/items?itemName=KarlShifflettkdawg.FontSizer
* CodeBlockEndTag: https://marketplace.visualstudio.com/items?itemName=KhaosPrinz.CodeBlockEndTag
* Remarker: https://marketplace.visualstudio.com/items?itemName=GilYoder.Remarker-18580
* CoCo: https://marketplace.visualstudio.com/items?itemName=GeorgeAleksandria.CoCo-19226