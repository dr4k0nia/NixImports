# NixImports
A .NET malware loader, using API-Hashing and dynamic invoking to evade static analysis

## How does it work?

NixImports uses my managed API-Hashing implementation HInvoke, to dynamically resolve most of it's called functions at runtime. 
To resolve the functions HInvoke requires two hashes the typeHash and the methodHash. These hashes represent the type name and the methods FullName, on runtime HInvoke parses the entire mscorlib to find the matching type and method.
Due to this process, HInvoke does not leave any import references to the methods called trough it. 

Another interesting feature of NixImports is that it avoids calling known methods as much as possible, whenever applicable NixImports uses
internal methods instead of their wrappers. By using internal methods only we can evade basic hooks and monitoring employed by some security tools.

For a more detailed explanation checkout [my blog post](https://dr4k0nia.github.io/posts/NixImports-a-NET-loader-using-HInvoke/).

You can generate hashes for HInvoke using [this tool](https://gist.github.com/dr4k0nia/813087cee2875f5f82e37c8a731b80b0)

## How to use

NixImports only requires a filepath to the .NET binary you want to pack with it.

```
NixImports.exe <filepath>
```

It will automatically generate a new executable called Loader.exe in it's root folder. The loader executable will contain your encoded payload and the stub code required to run it.


## Tips for Defenders

If youre interested in detection engineering and possible detection of NixImports, checkout [the last section of my blog post](https://dr4k0nia.github.io/posts/NixImports-a-NET-loader-using-HInvoke/#tips-for-defenders)
