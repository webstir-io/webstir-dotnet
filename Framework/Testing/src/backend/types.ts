export interface BackendTestContext {
  readonly baseUrl: string;
  readonly url: URL;
  readonly port: number;
  readonly manifest: unknown;
  readonly env: Readonly<Record<string, string>>;
  request(pathOrUrl?: string | URL, init?: RequestInit): Promise<Response>;
}

export interface BackendTestHarness {
  readonly context: BackendTestContext;
  stop(): Promise<void>;
}
