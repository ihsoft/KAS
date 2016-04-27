# Coding style
The sources of this mod follow a rather strict code style. Unified style improves code readability and makes maintaining much more easy. The style used is a derivative from [Google Java Style](http://google.github.io/styleguide/javaguide.html). C# (as of compiler version 5) and Java are very similar in many semantic constructions so, Java style fits to C# code very well. Though, there are some exceptions.

When making changes to the code, please, follow the code style-guide. Use common sense when something is not covered or following the style makes code looking weird. Though, the Google's style-guide is very comprehensive, and it's used by millions of Java-world people. If it doesn't cover your case then you're probably trying to do something unreasonable :)

# Commits into the trunk
* When you do a change try to break it down into as many small commits as possible. Reviewing of a small change is much easier.
* Mod must build and work correctly after each commit.
* Don't mix stye/re-factoring changes with the functional changes. I.e. it's either a functional change or a re-factoring change. Not the both.
* In the commit description always give the context.

# Main points of the code style
It's strongly suggested to read the code style publised on the Google' site. It's very detailed and has helpful examples. This section doesn't cover all the aspects, and only helps to understand the basics.

## C# specific requirements

###### Namespace indentation
Body of the namespace is not indented. With [one class per module requirement](http://google.github.io/styleguide/javaguide.html#s3.4-class-declaration) it's a waste of space. It's a waste even without this requirement.

```
namespace MyMod {

class MyClass {
  // ... the body
}

}  // namespace
```

###### Member names
All methods should follow [camel-case](http://google.github.io/styleguide/javaguide.html#s5.3-camel-case) rule. Method names start from a capitalized letter. Variables and fields start from a lower case letter (as in the Java style-guide).

```
class MyClass {
  bool myField;

  public void MyMethod() {
    var myVar = true;
  }
}
```

###### Member visibility
Don't place scope specifier for the private members. It's a default scope in C#.

Don't make members protected or public unless you have an intent to let other people overriding the class. Normally, any public class should be either declared `sealed` or have a section explaining how to override it. Classes that are only needed within the mod should be declared `internal`.

###### Documentation
Every public or protected method or class must have [a documentation section](https://msdn.microsoft.com/en-us/library/5ast78ax.aspx). Sometimes it makes sense to add comments for the private and internal members as well. It will help future maintainers to understand the mod's code.

Note, that overriden members still need documentation. If behavior is not changed, and there is nothing special to say about the overriden logic you may simply use `<inheritdoc/>` tag to refer to the parent's documentation.

## Common style requirements

* Absolutely no tabs! The code should look exactly the same in any editor with any settings.
* Everything wrapped in `{ }` must be indented by [exactly 2 spaces](http://google.github.io/styleguide/javaguide.html#s4.1.2-blocks-k-r-style). Opening bracket goes on the same line as the statement, and the closing bracket goes on own line.
* Maximum line length is 100 symbols. If the content doesn't fit then [wrap it](http://google.github.io/styleguide/javaguide.html#s4.5-line-wrapping).
 * When wrapping a conditional statement (e.g. in `if` statement) the operand goes on the next line with it's right argument. It's OK to indent wrapped statements logically (e.g. to match parenthesis groups).
* One file must have exactly [one top-level class](http://google.github.io/styleguide/javaguide.html#s3.4-class-declaration). The name of the module should match the name of the class.
* Not more than one statement per a line.
