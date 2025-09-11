# HTML Minification Implementation Plan

## Overview
Implement HTML minification for Webstir's publish workflow using a token-based approach similar to the CSS minification implementation.

## Phase 1: Basic HTML Tokenizer (2-3 days)

### Core Tokenization
- [ ] Create `Engine/Pipelines/Html/Tokenization/` directory structure
- [ ] Implement `HtmlTokenType.cs` enum with token types:
  - Doctype
  - OpenTag
  - CloseTag
  - SelfClosingTag
  - Text
  - Comment
  - CData
  - AttributeName
  - AttributeValue
  - Whitespace
- [ ] Implement `HtmlToken.cs` class with properties:
  - Type
  - Value
  - Position (line, column)
  - RawText (original representation)
- [ ] Create `HtmlTokenizer.cs`:
  - Parse HTML5 doctype declarations
  - Handle opening and closing tags
  - Parse self-closing tags and void elements
  - Tokenize attributes (names and values)
  - Handle text content between tags
  - Parse HTML comments (including IE conditionals)
  - Handle CDATA sections
  - Track source positions for error reporting

### Special Context Handling
- [ ] Implement special parsing for `<script>` content
- [ ] Implement special parsing for `<style>` content
- [ ] Preserve whitespace in `<pre>`, `<code>`, `<textarea>`
- [ ] Handle SVG and MathML namespaces

## Phase 2: Minification Rules (2-3 days)

### Core Minifier
- [ ] Create `HtmlTokenMinifier.cs`:
  - Remove HTML comments (except IE conditionals)
  - Collapse multiple whitespace to single space
  - Trim whitespace around block elements
  - Remove whitespace between tags where safe

### Attribute Optimization
- [ ] Remove unnecessary quotes from attribute values
- [ ] Collapse boolean attributes (`disabled="disabled"` → `disabled`)
- [ ] Remove empty attributes where safe
- [ ] Normalize attribute order alphabetically
- [ ] Convert attribute names to lowercase

### Element Optimization
- [ ] Remove optional closing tags (`</li>`, `</td>`, `</tr>`, `</th>`, `</p>`)
- [ ] Remove optional opening tags where applicable
- [ ] Collapse empty elements where safe
- [ ] Remove type="text/javascript" from script tags (HTML5 default)
- [ ] Remove type="text/css" from style tags (HTML5 default)

### Whitespace Preservation Rules
- [ ] Create whitelist for whitespace-sensitive elements
- [ ] Preserve single spaces between inline elements
- [ ] Maintain whitespace in inline event handlers
- [ ] Keep whitespace in data attributes

## Phase 3: Integration (1-2 days)

### Pipeline Integration
- [ ] Create `HtmlMinifier.cs` in `Engine/Pipelines/Html/Publish/`
- [ ] Add minification step to `PublishWorkflow` after HTML assembly
- [ ] Update `HtmlBundler.cs` to call minifier
- [ ] Add configuration options to `Constants.cs`:
  - EnableHtmlMinification (bool)
  - PreserveHtmlComments (bool)
  - RemoveOptionalTags (bool)
  - CollapseWhitespace (bool)

### Error Handling
- [ ] Implement graceful fallback on minification errors
- [ ] Add detailed error messages with file/line context
- [ ] Log minification statistics (before/after sizes)

## Phase 4: Testing and Validation (2-3 days)

### Unit Tests
- [ ] Create `Tests/Pipelines/Html/Tokenization/HtmlTokenizerTests.cs`:
  - Test each token type
  - Test malformed HTML handling
  - Test edge cases (unclosed tags, nested quotes)
- [ ] Create `Tests/Pipelines/Html/Tokenization/HtmlTokenMinifierTests.cs`:
  - Test each minification rule
  - Test whitespace preservation
  - Test attribute optimization
- [ ] Create `Tests/Pipelines/Html/HtmlSerializerTests.cs`:
  - Test round-trip parsing
  - Test output correctness

### Integration Tests
- [ ] Add HTML minification tests to `Tests/Workflows/Publish/`:
  - `HtmlMinificationReducesSize.cs`
  - `HtmlMinificationPreservesSemantics.cs`
  - `HtmlMinificationHandlesComplexPages.cs`
- [ ] Test with sample pages:
  - Forms with various input types
  - Tables with complex structures
  - Pages with inline scripts/styles
  - SVG and canvas elements
  - Template syntax preservation

### Visual Regression Tests
- [ ] Compare rendered output before/after minification
- [ ] Test in multiple browsers
- [ ] Verify no JavaScript errors introduced
- [ ] Check CSS still applies correctly

### Performance Tests
- [ ] Measure minification speed on large HTML files
- [ ] Compare file size reductions across sample pages
- [ ] Test gzip compression ratios before/after

## Success Metrics
- **Size Reduction**: Achieve 10-20% reduction in HTML file sizes
- **Performance**: Minification adds <100ms to publish time
- **Correctness**: Zero visual or functional differences
- **Reliability**: All existing tests pass with minification enabled

## Rollout Strategy
1. Implement behind feature flag (disabled by default)
2. Enable in development for internal testing
3. Run full test suite with minification enabled
4. Enable by default in next minor version
5. Provide per-page opt-out mechanism if needed

## Risk Mitigation
- Comprehensive test coverage before enabling
- Feature flag for quick rollback
- Clear documentation of any breaking changes
- Preserve original HTML for debugging if needed

## Dependencies
- Existing CSS minification implementation (reference)
- HTML5 specification for parsing rules
- Browser compatibility requirements

## Open Questions
- [ ] Should we preserve data-* attributes exactly as-is?
- [ ] How aggressive should we be with removing optional tags?
- [ ] Should we support custom preservation rules via configuration?
- [ ] Do we need to handle legacy HTML4/XHTML differently?

## Next Steps
1. Review and approve this plan
2. Set up development branch
3. Begin Phase 1 implementation
4. Schedule progress check-ins after each phase