import { bindValue } from "cs2/api";
import { TransitTransferSankeyData } from "./types";

export const transferData$ = bindValue<TransitTransferSankeyData>('TransitStats', 'transferSankeyData', {
    nodes: [],
    links: []
});