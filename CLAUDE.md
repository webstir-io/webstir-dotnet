# Webstir Framework Documentation

## 🚨 IMPORTANT: Check `.claude/` folder FIRST

**ALWAYS read the relevant `.claude/` documentation before making changes:**
- `.claude/Claude-README.md` - Overview and structure
- `.claude/Claude-Architecture.md` - Technical details  
- `.claude/Claude-Commands.md` - Command reference
- `.claude/Claude-Patterns.md` - **Code conventions and style guide**

This is a custom TypeScript framework built with .NET Core.

## Quick Start
1. Read `.claude/Claude-README.md` for project overview
2. Check `.claude/Claude-Patterns.md` for coding style
3. Review existing code before creating new files
4. Follow established patterns

## Post-Generation Review
After generating or modifying ANY code:
1. **Apply EVERY item in `.claude/Claude-Patterns.md`** - not just some
2. Fix ALL diagnostic issues shown by the IDE
3. Remove all WHAT comments (only keep WHY comments if needed)
4. Use Constants for ALL paths and magic strings
5. Use PathExtensions for ALL file/directory operations
6. Use explicit types everywhere (no var)
7. Apply collection expressions, GeneratedRegex, and other optimizations

**Do not submit code until ALL patterns are applied.**