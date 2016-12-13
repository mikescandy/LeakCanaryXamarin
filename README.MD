# LeakCanary
[LeakCanary](https://github.com/square/leakcanary) binding for xamarin

* [Why should I use LeakCanary?](#why-should-i-use-leakcanary)
* [How do I use it?](#how-do-i-use-it)
* [How does it work?](#how-does-it-work)
* [How do I customize LeakCanary to my needs?](#how-do-i-customize-leakcanary-to-my-needs)
* [How do I copy the leak trace?](#how-do-i-copy-the-leak-trace)
* [How do I fix a memory leak?](#how-do-i-fix-a-memory-leak)
* [Can a leak be caused by the Android SDK?](#can-a-leak-be-caused-by-the-android-sdk)
* [How can I dig beyond the leak trace?](#how-can-i-dig-beyond-the-leak-trace)
* [How do I fix build errors?](#how-do-i-fix-build-errors)
* [How many methods does LeakCanary add?](#how-many-methods-does-leakcanary-add)
* [Where can I learn more?](#where-can-i-learn-more)
* [How do I use the SNAPSHOT version?](#how-do-i-use-the-snapshot-version)
* [How can I be notified of new releases?](#how-can-i-be-notified-of-new-releases)
* [Why is it called LeakCanary?](#why-is-it-called-leakcanary)
* [Who made the logo?](#who-made-the-logo)
* [Build error: Failed to resolve](#build-error-failed-to-resolve)

## Why should I use LeakCanary?

Glad you ask! We wrote a [blog post](http://squ.re/leakcanary) to answer precisely that question.

## How do I use it?

Use a `RefWatcher` to watch references that should be GCed:

```cs
RefWatcher refWatcher = {...};

// We expect schrodingerCat to be gone soon (or not), let's watch it.
refWatcher.Watch(schrodingerCat);
```

`LeakCanary.Install()` returns a pre configured `RefWatcher`.
It also installs an `ActivityRefWatcher` that automatically detects if an activity is leaking after `Activity.OnDestroy()` has been called.

```cs
[Application]
public class MainApplication : Application
{
    private RefWatcher refWatcher;

    public static RefWatcher GetRefWatcher(Context context)
    {
        var application = (MainApplication)context.ApplicationContext;
        return application.refWatcher;
    }



    public MainApplication(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {

    }


    public override void OnCreate()
    {
        base.OnCreate();
        if (LeakCanaryXamarin.IsInAnalyzerProcess(this))
        {
            // This process is dedicated to LeakCanary for heap analysis.
            // You should not init your app in this process.
            return;
        }
        refWatcher = LeakCanaryXamarin.Install(this);
    }
}
 ```

You could use the `RefWatcher` to watch for fragment leaks:

```cs
public abstract class BaseFragment : Fragment
{
    public override void OnDestroy()
    {
        base.OnDestroy();
        var refWatcher = MainApplication.GetRefWatcher(Activity);
        refWatcher.Watch(this);
    }
}
```

## How does it work?

1. `RefWatcher.Watch()` creates a [KeyedWeakReference](https://github.com/square/leakcanary/blob/master/leakcanary-watcher/src/main/java/com/squareup/leakcanary/KeyedWeakReference.java) to the watched object.
2. Later, in a background thread, it checks if the reference has been cleared and if not it triggers a GC.
3. If the reference is still not cleared, it then dumps the heap into a `.hprof` file stored on the file system.
4. `HeapAnalyzerService` is started in a separate process and `HeapAnalyzer` parses the heap dump using [HAHA](https://github.com/square/haha).
5. `HeapAnalyzer` finds the `KeyedWeakReference` in the heap dump thanks to a unique reference key and locates the leaking reference.
6. `HeapAnalyzer` computes the *shortest strong reference path to the GC Roots* to determine if there is a leak, and then builds the chain of references causing the leak.
7. The result is passed back to `DisplayLeakService` in the app process, and the leak notification is shown.

## How do I customize LeakCanary to my needs?

See [Customizing LeakCanary](Customizing-LeakCanary).

## How do I copy the leak trace?

You can see the leak trace in Logcat:

```
In com.example.leakcanary:1.0:1 com.example.leakcanary.MainActivity has leaked:
* GC ROOT thread java.lang.Thread.<Java Local> (named 'AsyncTask #1')
* references com.example.leakcanary.MainActivity$3.this$0 (anonymous class extends android.os.AsyncTask)
* leaks com.example.leakcanary.MainActivity instance

* Reference Key: e71f3bf5-d786-4145-8539-584afaecad1d
* Device: Genymotion generic Google Nexus 6 - 5.1.0 - API 22 - 1440x2560 vbox86p
* Android Version: 5.1 API: 22
* Durations: watch=5086ms, gc=110ms, heap dump=435ms, analysis=2086ms
```

You can also share the leak trace and the heap dump file from the **action bar menu**.

## How do I fix a memory leak?

Once you have the leak trace, figure out which reference in the path should not exist. Then figure out why that reference still exists. Often times it's a registered listener that should have been unregistered, a `close()` method that wasn't called, an anonymous class that holds a reference an outer class. If you cannot figure out an issue in your code, **please do not file an issue**. Instead, create a [Stack Overflow question](http://stackoverflow.com/questions/tagged/leakcanary) (using the *leakcanary* tag).

## Can a leak be caused by the Android SDK?

Yes. There are a number of known memory leaks that have been fixed over time in AOSP as well as in manufacturer implementations. When such a leak occurs, there is little you can do as an app developer to fix it. For that reason, LeakCanary has a built-in list of known Android leaks to ignore: [AndroidExcludedRefs.java](https://github.com/square/leakcanary/blob/master/leakcanary-android/src/main/java/com/squareup/leakcanary/AndroidExcludedRefs.java).

If you find a new one, please [create an issue](https://github.com/square/leakcanary/issues/new) and follow these steps:

1. Provide the entire leak trace information (reference key, device, etc), and use backticks (`) for formatting.
2. Read the AOSP source for that version of Android, and try to figure out why it happens. You can easily navigate through SDK versions [android/platform_frameworks_base](https://github.com/android/platform_frameworks_base).
3. Check if it happens on the latest version of Android, and otherwise use blame to find when it was fixed.
4. If it's still happening, build a simple repro case
5. File an issue on [b.android.com](http://b.android.com) with the leak trace and the repro case
6. Create a PR in LeakCanary to update `AndroidExcludedRefs.java`. Optional: if you find a hack to clear that leak on previous versions of Android, feel free to document it.

This is especially important for **new releases of Android**. You have the opportunity to help detect new memory leaks early on, which benefits the entire Android community.

## How can I dig beyond the leak trace?

Sometimes the leak trace isn't enough and you need to dig into the heap dump with [MAT](http://eclipse.org/mat/) or [YourKit](https://www.yourkit.com/). Here's how you can find the leaking instance in the head dump:

1. Look for all instances of `com.squareup.leakcanary.KeyedWeakReference`
2. For each of these, look at the `key` field.
3. Find the `KeyedWeakReference` that has a `key` field equal to the reference key reported by LeakCanary.
4. The `referent` field of that `KeyedWeakReference` is your leaking object.
5. From then on, the matter is in your hands. A good start is to look at the shortest path to GC Roots (excluding weak references).

## How do I fix build errors?

* if `leakcanary-android` is not in the list of external libraries in Android Studio, but `leakcanary-analyzer` and `leakcanary-watcher` are there: try doing a `Clean Build`. If it's still a problem, try building from the command line.
* `error: package com.squareup.leakcanary does not exist`: if you have other build types than `debug` and `release`, you need to add a specific dependency for those too (`xxxCompile`).

## How many methods does LeakCanary add?

The short answer is **9**, or **0** if you use ProGuard. 

<a href="http://www.methodscount.com/?lib=com.squareup.leakcanary%3Aleakcanary-android-no-op%3A1.5"><img src="https://img.shields.io/badge/Methods and size-9 | 4 KB-e91e63.svg"></img></a>

LeakCanary should only be used in debug builds, and should be disabled in release builds. We provide a special empty dependency for your release builds: `leakcanary-android-no-op`.

The full version of LeakCanary is bigger and should never ship in your release builds.

<a href="http://www.methodscount.com/?lib=com.squareup.leakcanary%3Aleakcanary-android%3A1.5"><img src="https://img.shields.io/badge/Methods and size-core: 520 | deps: 2880 | 121 KB-e91e63.svg"></img></a>

## Where can I learn more?

* [GOTO 2016 - LeakCanary, then what? Nuking Nasty Memory Leaks - Pierre-Yves Ricau](https://www.youtube.com/watch?v=7A7Uv9ZBE2U)
* LeakCanary: Detect all memory leaks! [squ.re/leakcanary](http://squ.re/leakcanary).
* Wrangling Dalvik series: [Memory management in Android](http://www.raizlabs.com/dev/2014/03/wrangling-dalvik-memory-management-in-android-part-1-of-2/).
* Uploading leak traces to a [Slack or HipChat channel](https://gist.github.com/pyricau/06c2c486d24f5f85f7f0).
* Dump the heap on [OutOfMemoryError crashes](https://gist.github.com/pyricau/4726389fd64f3b7c6f32).
* StackOverflow: [leakcanary tag](http://stackoverflow.com/questions/tagged/leakcanary).



## Why is it called LeakCanary?

The name **LeakCanary** is a reference to the expression [canary in a coal mine](http://en.wiktionary.org/wiki/canary_in_a_coal_mine), because LeakCanary is a sentinel used to detect risks by providing advance warning of a danger. Props to [@edenman](https://github.com/edenman) for suggesting it!

