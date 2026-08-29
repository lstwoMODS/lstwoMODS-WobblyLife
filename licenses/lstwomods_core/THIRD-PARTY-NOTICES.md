# Third-Party Notices: lstwoMODS Core

lstwoMODS Core and the lstwoMODS overlay redistribute the third-party
components listed below. Each component remains under its own license and
copyright, held by its respective authors. The full text of every license used
is reproduced in the "License Texts" section at the end of this file.

Component versions and copyright lines were taken from the package metadata of
the exact builds that ship, or from the upstream project's own license file.

This file covers only what the lstwoMODS Core package ships. Modpacks built on
Core ship their own notices in sibling folders, for example
`licenses/lstwomods_wobblylife/`.

---

## Plugin folder (`BepInEx/plugins/lstwoMODS/`)

| Component | Files | License |
| --- | --- | --- |
| DynamicExpresso 2.19.3, Copyright (c) Davide Icardi, https://github.com/dynamicexpresso/DynamicExpresso | `DynamicExpresso.Core.dll` | MIT |
| Json.NET 13.0.3, Copyright (c) James Newton-King 2008, https://www.newtonsoft.com/json | `Newtonsoft.Json.dll` | MIT |
| Mono.Cecil 0.11.4, Copyright (c) 2008 - 2018 Jb Evain, https://github.com/jbevain/cecil | `Mono.Cecil.dll`, `Mono.Cecil.Mdb.dll`, `Mono.Cecil.Pdb.dll`, `Mono.Cecil.Rocks.dll` | MIT |
| Mono class libraries, Copyright (c) the Mono Project contributors (Novell, Xamarin, Microsoft and others), https://github.com/mono/mono | `Mono.Security.dll`, `System.dll`, `System.Core.dll`, `System.Xml.dll`, `System.Xml.Linq.dll`, `System.Runtime.Serialization.dll`, `System.ServiceModel.Internals.dll` | MIT |
| .NET compatibility packs, Copyright (c) Microsoft Corporation, https://github.com/dotnet/runtime | `System.Buffers.dll`, `System.Memory.dll`, `System.Runtime.CompilerServices.Unsafe.dll` | MIT |
| Unity UI (uGUI), Copyright (c) Unity Technologies, https://github.com/Unity-Technologies/uGUI | `UnityEngine.UI.dll` | Unity Companion License, see below |

## Overlay folder (`BepInEx/plugins/lstwoMODS/Overlay/`)

| Component | Files | License |
| --- | --- | --- |
| Hexa.NET bindings, Copyright (c) 2023 - 2025 Juna Meinhold, https://github.com/HexaEngine | `Hexa.NET.*.dll`, `HexaGen.Runtime.dll`, `HexaGen.Runtime.COM.dll` | MIT |
| Dear ImGui, Copyright (c) 2014-2026 Omar Cornut, https://github.com/ocornut/imgui | inside `cimgui.dll`, `ImGuiImpl.dll`, `ImGuiImplGLFW.dll` | MIT |
| cimgui, Copyright (c) 2015 Stephan Dilly, https://github.com/cimgui/cimgui | `cimgui.dll` | MIT |
| ImPlot, Copyright (c) 2020 Evan Pezent, https://github.com/epezent/implot | `cimplot.dll` | MIT |
| ImPlot3D, Copyright (c) 2024-2026 Breno Cunha Queiroz, https://github.com/brenocq/implot3d | `cimplot3d.dll` | MIT |
| imnodes, Copyright (c) 2019 Johann Muszynski, https://github.com/Nelarius/imnodes | `cimnodes.dll` | MIT |
| ImGuizmo, Copyright (c) 2016 Cedric Guillemet, https://github.com/CedricGuillemet/ImGuizmo | `cimguizmo.dll` | MIT |
| GLFW, Copyright (c) 2002-2006 Marcus Geelnard and (c) 2006-2019 Camilla Löwy, https://www.glfw.org | `glfw3.dll` | zlib/libpng |
| Json.NET 13.0.3, Copyright (c) James Newton-King 2008 | `Newtonsoft.Json.dll` | MIT |
| .NET libraries, Copyright (c) Microsoft Corporation, https://github.com/dotnet/runtime | `System.Text.Json.dll`, `System.Text.Encodings.Web.dll`, `System.IO.Pipelines.dll`, `System.Buffers.dll`, `System.Memory.dll`, `System.Numerics.Vectors.dll`, `System.Threading.Tasks.Extensions.dll`, `System.ValueTuple.dll`, `System.Runtime.CompilerServices.Unsafe.dll`, `Microsoft.Bcl.AsyncInterfaces.dll`, `Microsoft.Bcl.HashCode.dll`, `Microsoft.Bcl.Numerics.dll` | MIT |
| IndexRange 1.0.3, Copyright 2019-2023 Bradley Grainger, https://github.com/bgrainger/IndexRange | `IndexRange.dll` | MIT |
| Microsoft Visual C++ Runtime, Copyright (c) Microsoft Corporation | `vcruntime140.dll`, `vcruntime140_1.dll` | Microsoft redistributable, see below |
| Inter, Copyright (c) 2016 The Inter Project Authors, https://github.com/rsms/inter | `Assets/InterVariable.ttf` | SIL Open Font License 1.1 |
| Lucide, Copyright (c) Lucide Icons and Contributors, https://lucide.dev | `Assets/lucide.ttf` | ISC, with Feather under MIT |

---

# License Texts

## MIT License

Applies to every component marked MIT above, with copyright held by the
respective holders named there:

```
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## zlib/libpng License (GLFW)

```
Copyright (c) 2002-2006 Marcus Geelnard
Copyright (c) 2006-2019 Camilla Löwy

This software is provided 'as-is', without any express or implied warranty. In
no event will the authors be held liable for any damages arising from the use
of this software.

Permission is granted to anyone to use this software for any purpose,
including commercial applications, and to alter it and redistribute it freely,
subject to the following restrictions:

1. The origin of this software must not be misrepresented; you must not claim
   that you wrote the original software. If you use this software in a
   product, an acknowledgment in the product documentation would be
   appreciated but is not required.

2. Altered source versions must be plainly marked as such, and must not be
   misrepresented as being the original software.

3. This notice may not be removed or altered from any source distribution.
```

## ISC License (Lucide)

Lucide is a fork of Feather, which is MIT licensed. Both notices apply.

```
ISC License

Copyright (c) for portions of Lucide are held by Cole Bemis 2013-2022 as part
of Feather (MIT). All other copyright (c) for Lucide are held by Lucide
Contributors 2022.

Permission to use, copy, modify, and/or distribute this software for any
purpose with or without fee is hereby granted, provided that the above
copyright notice and this permission notice appear in all copies.

THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES WITH
REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF MERCHANTABILITY
AND FITNESS. IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY SPECIAL, DIRECT,
INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES WHATSOEVER RESULTING FROM
LOSS OF USE, DATA OR PROFITS, WHETHER IN AN ACTION OF CONTRACT, NEGLIGENCE OR
OTHER TORTIOUS ACTION, ARISING OUT OF OR IN CONNECTION WITH THE USE OR
PERFORMANCE OF THIS SOFTWARE.
```

The Feather MIT notice, Copyright (c) 2013-present Cole Bemis, is covered by
the MIT License text above.

## SIL Open Font License 1.1 (Inter)

```
Copyright (c) 2016 The Inter Project Authors (https://github.com/rsms/inter)

This Font Software is licensed under the SIL Open Font License, Version 1.1.

-----------------------------------------------------------
SIL OPEN FONT LICENSE Version 1.1 - 26 February 2007
-----------------------------------------------------------

PREAMBLE
The goals of the Open Font License (OFL) are to stimulate worldwide
development of collaborative font projects, to support the font creation
efforts of academic and linguistic communities, and to provide a free and open
framework in which fonts may be shared and improved in partnership with
others.

The OFL allows the licensed fonts to be used, studied, modified and
redistributed freely as long as they are not sold by themselves. The fonts,
including any derivative works, can be bundled, embedded, redistributed and/or
sold with any software provided that any reserved names are not used by
derivative works. The fonts and derivatives, however, cannot be released under
any other type of license. The requirement for fonts to remain under this
license does not apply to any document created using the fonts or their
derivatives.

DEFINITIONS
"Font Software" refers to the set of files released by the Copyright Holder(s)
under this license and clearly marked as such. This may include source files,
build scripts and documentation.

"Reserved Font Name" refers to any names specified as such after the copyright
statement(s).

"Original Version" refers to the collection of Font Software components as
distributed by the Copyright Holder(s).

"Modified Version" refers to any derivative made by adding to, deleting, or
substituting -- in part or in whole -- any of the components of the Original
Version, by changing formats or by porting the Font Software to a new
environment.

"Author" refers to any designer, engineer, programmer, technical writer or
other person who contributed to the Font Software.

PERMISSION AND CONDITIONS
Permission is hereby granted, free of charge, to any person obtaining a copy
of the Font Software, to use, study, copy, merge, embed, modify, redistribute,
and sell modified and unmodified copies of the Font Software, subject to the
following conditions:

1) Neither the Font Software nor any of its individual components, in Original
or Modified Versions, may be sold by itself.

2) Original or Modified Versions of the Font Software may be bundled,
redistributed and/or sold with any software, provided that each copy contains
the above copyright notice and this license. These can be included either as
stand-alone text files, human-readable headers or in the appropriate
machine-readable metadata fields within text or binary files as long as those
fields can be easily viewed by the user.

3) No Modified Version of the Font Software may use the Reserved Font Name(s)
unless explicit written permission is granted by the corresponding Copyright
Holder. This restriction only applies to the primary font name as presented to
the users.

4) The name(s) of the Copyright Holder(s) or the Author(s) of the Font
Software shall not be used to promote, endorse or advertise any Modified
Version, except to acknowledge the contribution(s) of the Copyright Holder(s)
and the Author(s) or with their explicit written permission.

5) The Font Software, modified or unmodified, in part or in whole, must be
distributed entirely under this license, and must not be distributed under any
other license. The requirement for fonts to remain under this license does not
apply to any document created using the Font Software.

TERMINATION
This license becomes null and void if any of the above conditions are not met.

DISCLAIMER
THE FONT SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
OR IMPLIED, INCLUDING BUT NOT LIMITED TO ANY WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT OF COPYRIGHT, PATENT,
TRADEMARK, OR OTHER RIGHT. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE
FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, INCLUDING ANY GENERAL, SPECIAL,
INDIRECT, INCIDENTAL, OR CONSEQUENTIAL DAMAGES, WHETHER IN AN ACTION OF
CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF THE USE OR INABILITY TO USE
THE FONT SOFTWARE OR FROM OTHER DEALINGS IN THE FONT SOFTWARE.
```

## Unity Companion License (UnityEngine.UI)

`UnityEngine.UI.dll` is Unity's uGUI module, taken from a Unity installation so
that the in-game UI can bind against it. Unity publishes the uGUI sources under
the Unity Companion License:
https://unity.com/legal/licenses/unity-companion-license

The assembly is Copyright (c) Unity Technologies and is redistributed
unmodified, as a runtime dependency of a mod for a Unity game.

## Microsoft Visual C++ Runtime (`vcruntime140.dll`, `vcruntime140_1.dll`)

Copyright (c) Microsoft Corporation. These are the unmodified Microsoft Visual
C++ redistributable runtime files, shipped app-local because every Hexa.NET
native library imports them and they are not part of Windows. They are
redistributed under the redistributable-code terms of the Microsoft Visual
Studio license:
https://visualstudio.microsoft.com/license-terms/