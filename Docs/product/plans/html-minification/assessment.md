# HTML Minification Assessment

## Overview
This document assesses the implementation of HTML minification in Webstir's publish workflow, following the successful CSS minification approach.

## Current State
- HTML files are assembled from templates (app.html + page fragments)
- No minification currently applied
- Published HTML retains all formatting, comments, and whitespace

## Proposed Approach

### Token-Based Minification
Similar to CSS, use a token-based approach to preserve correctness:
1. **HtmlTokenizer**: Parse HTML into semantic tokens
2. **HtmlTokenMinifier**: Apply safe minification rules
3. **HtmlSerializer**: Reconstruct minified HTML

### Safe Optimizations
- Remove HTML comments (preserve IE conditionals if needed)
- Collapse whitespace between tags
- Trim whitespace around block-level elements
- Remove unnecessary quotes from attribute values
- Collapse boolean attributes (`disabled="disabled"` → `disabled`)
- Remove optional closing tags where safe (e.g., `</li>`, `</td>`)
- Normalize attribute order for better gzip compression

### Preserve Semantics In
- `<pre>`, `<code>`, `<textarea>` - preserve exact whitespace
- `<script>` tags - don't modify (handled by JS minifier)
- `<style>` tags - don't modify (handled by CSS minifier)
- Inline event handlers - preserve JavaScript as-is
- Data attributes - preserve values exactly

## Implementation Phases

### Phase 1: Basic Tokenizer
- Create `Engine/Pipelines/Html/Tokenization/HtmlTokenizer.cs`
- Support core HTML5 elements and attributes
- Handle self-closing tags and void elements
- Parse comments, CDATA sections, and doctypes

### Phase 2: Minification Rules
- Create `Engine/Pipelines/Html/Tokenization/HtmlTokenMinifier.cs`
- Implement whitespace collapsing rules
- Remove comments (except conditionals)
- Handle attribute optimization

### Phase 3: Integration
- Add to `PublishWorkflow` after HTML assembly
- Create `HtmlMinifier.cs` in `Engine/Pipelines/Html/Publish/`
- Add configuration options to control minification level

## Expected Benefits
- **Size Reduction**: 10-20% typical reduction
- **Faster Parse Time**: Less HTML for browser to process
- **Network Savings**: Reduced bandwidth usage
- **Better Compression**: Normalized structure compresses better

## Risks and Mitigations

### Risk: Breaking Inline Whitespace
**Mitigation**: Preserve single spaces between inline elements, use allowlist for elements requiring exact whitespace

### Risk: JavaScript String Literals
**Mitigation**: Don't modify content within `<script>` tags or inline event handlers

### Risk: CSS Selectors Depending on HTML Structure  
**Mitigation**: Don't remove "unnecessary" elements/attributes that might be CSS targets

### Risk: Third-Party Integrations
**Mitigation**: Provide option to disable minification per-page or globally

## Success Criteria
- [ ] All existing tests pass with minification enabled
- [ ] No visual differences in rendered pages
- [ ] Clear error messages if minification fails

## Testing Strategy
1. Create test cases for each token type
2. Test whitespace preservation in sensitive contexts
3. Verify no semantic changes in output
4. Compare rendered output before/after minification
5. Test with real-world HTML including:
   - Complex forms
   - Nested tables
   - SVG content
   - Inline scripts/styles
   - Template syntax (if applicable)

## Timeline Estimate
- Phase 1: 2-3 days (tokenizer)
- Phase 2: 2-3 days (minification rules)
- Phase 3: 1-2 days (integration)
- Testing: 2-3 days
- **Total: 7-11 days**

## Decision
**Recommendation**: Proceed with implementation following the token-based approach proven successful with CSS minification.

## Next Steps
1. Review and approve this assessment
2. Create implementation plan with detailed token types
3. Begin Phase 1 with HtmlTokenizer
4. Create comprehensive test suite