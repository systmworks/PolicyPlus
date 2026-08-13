# Policy Plus
Local Group Policy Editor plus more, for all Windows editions.

[![Build Latest](https://github.com/systmworks/PolicyPlus/actions/workflows/latest.yml/badge.svg)](https://github.com/systmworks/PolicyPlus/actions/workflows/latest.yml)

## Goals
Policy Plus is intended to make the power of Group Policy settings available to everyone.

* Run and work on all Windows editions, not just Pro and Enterprise
* Comply fully with licensing (i.e. transplant no components across Windows installations)
* View and edit Registry-based policies in local GPOs, per-user GPOs, individual POL files, offline Registry user hives, and the live Registry
* Navigate to policies by ID, text, or affected Registry entries
* Show additional technical information about objects (policies, categories, products)
* Provide convenient ways to share and import policy settings

Non-Registry-based policies (i.e. items outside the Administrative Templates branch of the Group Policy Editor) currently have no priority, 
but they may be reconsidered at a later date.

## Quick intro
At startup, Policy Plus opens the last saved policy source, or the local Group Policy Object (Local GPO) by default.
To open a different policy source (like a Registry branch or a per-user GPO), use *File | Open Policy Resources*.

Much like the official Group Policy editor, categories are shown in the left tree.
Information on the selected object is shown in the middle.
Policies and subcategories in the selected category are shown in the right list.
By default, both user and computer policies are displayed, but you can focus on just one policy source using the drop-down in the upper left.

To edit a policy, double-click it. If the selected setting applies to both users and computers,
you can switch sections with the "Editing for" drop-down. Click OK to keep the changes to the setting.
**Notice:** If a policy source is backed by a POL file (like Local GPO),
changes to it will not be committed to disk until you use *File | Save Policies* (Ctrl+S).

## System requirements
Policy Plus requires the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) or newer.
See [INSTALL.md](INSTALL.md) for details.

## Special considerations for use on Home editions
Some administrative templates are present by default on these editions, but many are missing. 
The newest full package can be downloaded from Microsoft and installed with *Help | Acquire ADMX Files*.

The `RefreshPolicyEx` native function has reduced functionality on editions without full Group Policy infrastructure,
so while Policy Plus can edit the local GPO and apply the changes to the Registry, 
a reboot or logon/logoff cycle is required for some policy changes to take effect.

When saving User policies, the simulated policy refresh from the local GPO to the Registry is done only for the current user.
Similarly, editing per-user local GPOs (a fairly arcane Windows feature not to be confused with the User section),
has no effect on these limited editions of Windows.
To change a policy for a different user, modify their Registry directly by opening a "user hive" or "local Registry" source.

## Status
Policy Plus is usable on all editions. It can load and save all policy sources successfully. More features may be still to come, though.

## Download
This fork doesn't currently publish tagged releases. Every push to `master` is built
automatically by [GitHub Actions](https://github.com/systmworks/PolicyPlus/actions/workflows/latest.yml) —
open the latest successful "Build Latest" run and download the **Policy Plus (Windows)**
artifact from the Summary page. It's built straight from the code in this repo, so it's
only as tested as the commit it came from.

**N.B.** A few antivirus programs incorrectly flag Policy Plus as malware, and since the
executable isn't code-signed, Windows SmartScreen will show an "unrecognized app" warning
on first launch (click *More info* → *Run anyway*). Policy Plus is a powerful tool and so
may cause problems if used recklessly, but it is not malicious.
If you would prefer to not trust binaries, feel free to read the code and [compile Policy Plus from source](COMPILE.md).
You can also verify that a build was created from the published code by examining the output of a GitHub Actions run:
the input commit hash can be found under "checkout master" and the output executable hash can be found under "compute hash."
