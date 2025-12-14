export type RouteParams = Record<string, string | undefined>;

export type RouteHandler = {
    onEnter?: (params: RouteParams) => void | Promise<void>;
    onLeave?: () => void | Promise<void>;
};
