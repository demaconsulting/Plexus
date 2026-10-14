# Introduction

This document contains the requirements for the Plexus project.

## Purpose

Plexus is a general-purpose .NET library for building and editing node-based block diagrams. It ships a
UI-free core (`DemaConsulting.Plexus`), a headless PNG renderer (`DemaConsulting.Plexus.Rendering`) and an
Avalonia editor control (`DemaConsulting.Plexus.Avalonia`). The requirements are organized by software system
(Plexus, PlexusRendering and PlexusAvalonia), subsystem and unit, and are traced to automated tests.

## Scope

This requirements document covers:

- The Plexus core system: document model, undo history, node types, properties, routing, scene graph and
  clipboard serialization
- The PlexusRendering system: headless PNG rendering
- The PlexusAvalonia system: the interactive Avalonia editor control
- Multi-platform and multi-runtime support
- Off-the-shelf (OTS) software items used by the libraries and by the build and compliance pipeline

## Audience

This document is intended for:

- Software developers working on Plexus
- Quality assurance teams validating requirements
- Project stakeholders reviewing project scope
- Users understanding the library's capabilities
