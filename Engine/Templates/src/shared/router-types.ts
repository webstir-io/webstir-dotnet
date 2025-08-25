export interface RouteHandler {
  onEnter?: (params: RouteParams) => void | Promise<void>;
  onLeave?: () => void | Promise<void>;
  onUpdate?: (params: RouteParams) => void | Promise<void>;
}

export interface RouteParams {
  [key: string]: string;
}

export interface RoutingMetadata {
  pages: {
    [pageName: string]: PageRouteInfo;
  };
  hasSpaPages: boolean;
}

export interface PageRouteInfo {
  pageName: string;
  route: string;
  isSpaEnabled: boolean;
  typeScriptPath: string;
}