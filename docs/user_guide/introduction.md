# Introduction

## Purpose

This document is the user guide for Plexus, a general-purpose .NET library for building and editing node-based block
diagrams with typed ports, routed wires, annotations, nestable collapsible groups, multiple pages, full undo/redo, and
cross-application copy/paste.

Plexus is aimed at many independent host applications. The host decides what the nodes are (hardware blocks, signal
processing stages, workflow steps) and owns file formats; Plexus provides the
diagram model, editing behavior, rendering and an interactive editor control. The guide shows how a host application
uses each of those parts, task by task, and keeps every task short: if a common task needs a page of code, that is a
defect of the library to be reported, not something to document.

![The Plexus demo application showing a multi-page diagram](images/overview.png)

## Scope

This user guide covers:

- The packages and how to install them
- Core concepts: diagrams, pages, nodes, ports, wires, annotations and groups
- Declaring node types, ports, properties, categories, icons and host data
- Building and editing diagrams in code: pages, connections, groups, annotations, move to page
- Undo, redo, transactions and dirty tracking
- Saving and loading diagrams as JSON, the snapshot model, custom file formats (a TOML example with Tomlyn) and the
  host's own objects stored with the diagram, and the clipboard between applications
- Headless rendering to PNG
- Embedding the Avalonia editor control and the node palette
- The property grid: a generic, annotation-driven Avalonia grid, with a ready-made node properties dialog and a docked
  panel whose every commit is one undo step
- Appearance: themes and dynamic wire and node styling
- Wires: connection rules, fan-in, routing, wire editing and custom routers
- Extension points: custom node visuals, the scene and hit testing, custom serializers
- Testing and automating a host application

The library does not load or save files (the host owns file formats), ship a node-type catalog or icon library,
or support wires between pages. A property-editing grid is a separate package.

## How to Read This Guide

The guide builds on one running example, a small math dataflow (constants feeding an adder that feeds a scope), which
mirrors the Math domain of the demo application shipped in the repository. Fragments continue from the objects built
in *Getting Started*: `types` (the node type registry), `diagram`, `page` and the nodes `a`, `b`, `sum` and `scope`.
A fragment that needs a namespace the previous ones did not use starts with its `using` lines; the other fragments
assume the namespaces already imported in the same chapter. The code of the guide is compiled against the current
sources of the library.

Plexus has a complete reference host application, the demo, in the `demo/` folder of the repository. Every feature in
this guide is used there, and the screenshots are taken from it. *Getting Started* explains how to run it, and the
chapter *Testing and Automating* explains how it is driven by scripts.

The packages ship a Markdown API reference (see *API Reference* in *Getting Started*) that lists every member; this
guide explains how the parts fit together.

## Packages

Plexus is delivered as four NuGet packages:

| Package                                       | Purpose                                                              |
|-----------------------------------------------|----------------------------------------------------------------------|
| `DemaConsulting.Plexus`                       | UI-free core: model, undo, routing, scene graph, clipboard format    |
| `DemaConsulting.Plexus.Rendering`             | Headless PNG renderer (depends on CanvasNet)                         |
| `DemaConsulting.Plexus.Avalonia`              | Avalonia editor control and node palette (depends on Avalonia)       |
| `DemaConsulting.Plexus.Avalonia.PropertyGrid` | Property grid, properties dialog and panel (depends on Avalonia)     |

The core targets .NET 8, 9 and 10 and has no third-party runtime dependencies. The other packages target .NET 10. The
Avalonia package does not depend on the rendering package, and the property grid package is generic: it edits any
annotated .NET object and adds a small adapter for Plexus nodes.

## Continuous Compliance

Plexus follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run. Requirements are traced to passing tests, markdownlint,
cspell and yamllint are enforced before any build proceeds, and each release ships with generated requirements, trace
matrix, design, verification and quality documents.

## References

- [REF-1] Continuous Compliance Methodology (<https://github.com/demaconsulting/ContinuousCompliance>)
- [REF-2] Avalonia UI Documentation (<https://docs.avaloniaui.net>)
