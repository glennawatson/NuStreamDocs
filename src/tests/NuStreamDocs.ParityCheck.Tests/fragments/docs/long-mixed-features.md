Mixed Feature Document
======================

This document combines many *basic* Markdown features in one page. It has a setext title,
paragraphs that wrap across lines, and a few `inline code` spans.  
A hard break precedes this line, and this one ends with a backslash\
so another break follows.

Section With ATX Heading
------------------------

A paragraph with a [link](http://example.com "Example"), an ![image](/img/x.png "Image"),
an autolink <https://example.com/auto>, and an email <someone@example.com>.

### Third-level heading ###

> A block quote that spans
> several lines and contains **strong** text.
>
> - a list inside the quote
> - with two items
>
> > A nested quote inside the quote.

#### Lists

* Bullet one
* Bullet two
    * Nested bullet
    * Another nested bullet
        * Deep bullet
* Bullet three

An ordered list follows:

1. Ordered one
2. Ordered two
    1. Nested ordered
    2. Second nested ordered
3. Ordered three

A loose list follows:

- Loose item one

- Loose item two with a second paragraph:

    The second paragraph of the loose item.

- Loose item three

##### Code

Indented code:

    def hello():
        return "world"

Fenced code:

```python
def hello():
    return "world"
```

Tilde fenced:

~~~
plain text <b>not bold</b> &amp; not decoded
~~~

###### Raw HTML

<div class="custom">
A raw HTML block, left as is.
</div>

Inline <kbd>Ctrl</kbd>+<kbd>C</kbd> and an HTML comment <!-- hidden --> in a line.

***

Reference links: [first][one], [second][two], and the [shortcut].

[one]: http://example.com/one
[two]: http://example.com/two "Second"
[shortcut]: http://example.com/shortcut

Escapes: \*not emphasis\*, \_not either\_, \`not code\`, 1\. not a list, \# not a heading.

Entities: &copy; &amp; &lt;tag&gt; &#169; &#x263A;.

Emphasis edge cases: snake_case_name, 2*3*4, **bold**text, *a*b, and an unmatched *star.

---

Final paragraph without trailing newline.