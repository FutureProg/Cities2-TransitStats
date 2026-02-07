export interface SankeyNode {
    id: string;
    name: string;
    iconUrl: string;
    color: string;
}

export  interface SankeyLink {
    source: string;
    target: string;
    value: number;
    color: string;
}

export interface TransitTransferSankeyData {
    nodes: SankeyNode[];
    links: SankeyLink[];
}