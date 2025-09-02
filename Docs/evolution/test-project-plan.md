# Test Project Plan

## Overview
This document outlines a comprehensive testing strategy for testing the WebStir build tool itself, without relying on third-party testing frameworks. The plan focuses on validating WebStir's core functionality: compilation, building, watching, and publishing of user projects.

## Current Testing Infrastructure ✅ IMPLEMENTED

### Test Framework (Complete)
- **Location**: `Tests/` directory with full framework structure
- **Framework**: Custom test framework with dependency injection
- **Architecture**: Modular test suites with base classes and interfaces
- **Output**: Multiple formats (Console, JSON, XML, Markdown) with clean rollup summary
- **Coverage**: 13 focused tests covering core WebStir functionality

### Framework Features
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection for extensible architecture
- **Custom Assertions**: Assert helper class with comprehensive validation methods
- **Test Organization**: Category-based test suites with filtering support
- **Clean Output**: Rollup format showing "All tests passed" or failure details only
- **Multiple Formats**: Console, JSON, XML, and Markdown output options
- **Result Management**: Organized output to `Tests/results/{category}/` structure

## Proposed Testing Strategy

### 1. Test Execution Strategy ✅ IMPLEMENTED
Tests run independently from the main WebStir CLI:

```bash
# Run all tests (current implementation)
dotnet run --project Tests                    # All 13 tests
dotnet run --project Tests css                # CSS-specific tests
dotnet run --project Tests workers            # Worker tests
dotnet run --project Tests services           # Service tests
dotnet run --project Tests integration        # Integration tests

# Output options
dotnet run --project Tests --output results.json    # JSON output
dotnet run --project Tests --output results.md      # Markdown output
dotnet run --project Tests --help                   # Show help
```

### 2. Test Categories

#### A. Unit Tests (WebStir Components) ✅ IMPLEMENTED
- **CSS Processors** (3 tests)
  - ✅ CssMinifier validation with real-world CSS performance testing
  - ✅ CssImportProcessor @import resolution and file handling
  - ✅ CssPathResolver namespace handling (@app/, @components/)
- **Workers** (5 tests)
  - ✅ Worker instantiation and build order validation
  - ✅ ProjectMode enum handling across all workers
  - ✅ Worker categories and BuildOrder sequence verification
- **Services** (2 tests)
  - ✅ WebServer: Kestrel server instantiation and initial state
  - ✅ NodeService: Process management initialization

#### B. Integration Tests (WebStir Build Pipeline) ✅ PARTIALLY IMPLEMENTED
- **Component Integration** (3 tests)
  - ✅ WebServer and NodeService integration validation
  - ✅ CSS processor pipeline with Workers integration
  - ✅ Service dependencies and instantiation flow
- **Build Commands** (Future phases)
  - Complete build process: `dotnet run -- build`
  - Watch mode file change detection and hot reload
  - Publish command production optimization

#### C. End-to-End Tests (Complete WebStir Workflows)
- **Demo Creation**
  - `dotnet run -- demo testproject` execution
  - Generated project structure validation
  - Template resource embedding accuracy
- **Full Development Cycle**
  - Create demo → build → watch → publish workflow
  - Verify each step produces expected outputs
  - Test error handling and recovery

#### D. Performance Tests (WebStir Build Tool Benchmarking)
- **Build Performance**
  - Build command execution time
  - Memory usage during compilation
  - File I/O efficiency (copying, writing)
- **Watch Mode Performance**
  - File change detection latency
  - Incremental build speed
  - WebSocket communication overhead
- **Publish Performance**
  - Production build optimization time
  - CSS inlining and minification speed

### 3. Test Framework Design

#### Core Testing Infrastructure
```csharp
// Test runner base classes
public abstract class TestSuite
{
    public abstract string Name { get; }
    public abstract Task<TestResult[]> RunAsync();
}

public class TestResult
{
    public string TestName { get; set; }
    public bool Passed { get; set; }
    public string Message { get; set; }
    public TimeSpan Duration { get; set; }
    public Exception Exception { get; set; }
}

// Assertion helpers
public static class Assert
{
    public static void IsTrue(bool condition, string message = "")
    public static void AreEqual<T>(T expected, T actual, string message = "")
    public static void IsNotNull(object value, string message = "")
    public static void Throws<T>(Action action, string message = "") where T : Exception
}
```

#### Test Discovery and Execution
- Automatic test suite discovery using reflection
- Parallel test execution where safe
- Detailed reporting with timing and memory usage
- Test categorization and filtering

### 4. TypeScript/JavaScript Testing

#### Client-Side Component Tests
```typescript
// Simple test framework for TypeScript
interface TestCase {
    name: string;
    test: () => Promise<void> | void;
}

class TestRunner {
    private tests: TestCase[] = [];
    
    add(name: string, test: () => Promise<void> | void) {
        this.tests.push({ name, test });
    }
    
    async run(): Promise<TestResult[]> {
        // Execute tests and return results
    }
}

// Example usage
const runner = new TestRunner();
runner.add('Router navigation', () => {
    // Test router functionality
});
runner.add('Component rendering', () => {
    // Test DOM manipulation
});
```

#### Browser Automation (Without External Tools)
- Use C# WebView2 or similar for browser testing
- Headless browser execution for CI/CD
- DOM manipulation validation
- Event handling verification

### 5. File System Testing

#### Template and Resource Validation
- Verify embedded resources are accessible
- Test file copying and path resolution
- Validate generated project structures
- Check file permissions and attributes

#### Build Output Verification
- Compare expected vs actual file contents
- Validate CSS/JS minification results
- Test asset bundling accuracy
- Verify source map generation

### 6. Implementation Plan

#### Phase 1: Core Infrastructure
1. Expand `Tests/Program.cs` with test framework classes
2. Create base test suite classes and test runner
3. Implement assertion helpers
4. Add command-line argument parsing for test categories

#### Phase 2: Unit Tests
1. CSS processor tests (expand existing)
2. Worker component tests
3. Service layer tests
4. Helper utility tests

#### Phase 3: Integration Tests
1. Build pipeline tests
2. Watch mode tests
3. WebServer integration tests
4. Node.js process management tests

#### Phase 4: End-to-End Tests
1. Demo creation workflow
2. Complete development workflow
3. Production build workflow
4. Error handling scenarios

#### Phase 5: Performance & Reporting
1. Performance benchmarking
2. Test result reporting
3. CI/CD integration
4. Coverage analysis (manual tracking)

### 7. Test Data Management

#### Sample Projects
Create minimal test projects for different scenarios:
- Basic single-page application
- Multi-page application with routing
- Application with CSS imports
- Application with complex TypeScript

#### Mock Data
- Sample CSS files for processor testing
- HTML templates for markup testing
- TypeScript files for compilation testing
- Configuration files for settings testing

### 8. Continuous Integration

#### Pre-commit Validation
- Run critical tests before allowing commits
- Validate build process integrity
- Check for breaking changes

#### Build Pipeline Integration
- Automated test execution on builds
- Performance regression detection
- Test result artifacts

### 9. Documentation and Maintenance

#### Test Documentation
- Document test categories and purposes
- Maintain test data requirements
- Update test procedures for new features

#### Maintenance Strategy
- Regular test review and updates
- Performance baseline updates
- Test coverage gap analysis

## Benefits of This Approach

1. **No External Dependencies**: Pure .NET and TypeScript implementation
2. **Tight Integration**: Tests use same infrastructure as main application
3. **Performance Focus**: Built-in benchmarking and optimization validation
4. **Comprehensive Coverage**: All layers from unit to end-to-end testing
5. **Developer Friendly**: Simple command-line interface for running tests
6. **CI/CD Ready**: Easy integration with build pipelines

## Implementation Timeline

- **Week 1**: Core test infrastructure and CSS tests expansion
- **Week 2**: Worker and service unit tests
- **Week 3**: Integration tests for build pipeline
- **Week 4**: End-to-end workflow tests and performance benchmarking

## Implementation Status

### Phase 1: Core Infrastructure ✅ COMPLETED
- ✅ Expanded Tests/Program.cs with full test framework
- ✅ Created base test suite classes and test runner with DI
- ✅ Implemented comprehensive assertion helpers
- ✅ Added command-line argument parsing for test categories and output formats

### Phase 2: Unit Tests ✅ COMPLETED  
- ✅ CSS processor tests (CssMinifier, CssImportProcessor, CssPathResolver)
- ✅ Worker component tests (instantiation, build order, ProjectMode)
- ✅ Service layer tests (WebServer, NodeService initialization)
- ⚠️ Helper utility tests (skipped - deemed unnecessary per user preference)

### Phase 3: Integration Tests ✅ COMPLETED
- ✅ Component integration tests (service dependencies, CSS pipeline)
- ✅ WebServer and NodeService integration validation
- ✅ Cross-component instantiation and state verification

### Phase 4: End-to-End Tests ⏸️ PENDING (Low Priority)
- ⏸️ Demo creation workflow tests
- ⏸️ Complete development workflow tests  
- ⏸️ Production build workflow tests
- ⏸️ Error handling scenario tests

### Phase 5: Performance & Reporting ⏸️ PENDING (Low Priority)
- ⏸️ Performance benchmarking
- ✅ Test result reporting (multiple formats implemented)
- ⏸️ CI/CD integration
- ⏸️ Coverage analysis (manual tracking)

## Current Status Summary
**13 focused tests implemented** covering essential WebStir functionality:
- Clean rollup output format ("All tests passed" vs failure details)
- Multiple output formats (Console, JSON, XML, Markdown)
- Focused testing approach (no over-testing per user preference)
- Extensible architecture ready for future phases if needed

This implementation provides a solid testing foundation that validates WebStir's core functionality while maintaining simplicity and avoiding external dependencies.