// Demo-specific type definitions

export interface User {
    id: string;
    name: string;
    email: string;
}

export interface Feature {
    id: string;
    name: string;
    description: string;
}

export interface Post {
    id: string;
    title: string;
    content: string;
    authorId: string;
}