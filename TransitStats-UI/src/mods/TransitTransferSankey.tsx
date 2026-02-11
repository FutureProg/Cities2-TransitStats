// File: transit-transfer-sankey.tsx
import React, { useEffect, useMemo, useRef } from "react";
import {
    ResponsiveSankey,
    Sankey,
    SankeyCustomLayer,
    SankeyLabelComponent,
} from "@nivo/sankey";
import { useValue } from "cs2/api";
import { transferData$ } from "./bindings";
import { SankeyLink, SankeyNode, TransitTransferSankeyData } from "./types";
import { Tooltip } from "cs2/ui";

interface TransitTransferSankeyProps {
    className?: string;
}

export const TransitTransferSankey = (
    { className }: TransitTransferSankeyProps,
) => {
    let svgRef = useRef<SVGSVGElement>(null);
    // Bind to game system data
    const sankeyData = useValue<TransitTransferSankeyData>(transferData$);

    const data = useMemo(() => {
        if (!sankeyData || !sankeyData.nodes || !sankeyData.links) {
            return { nodes: [], links: [] };
        }
        return sankeyData;
    }, [sankeyData]);

    if (data.nodes.length === 0) {
        return (
            <div className={className}>
                <p>No transfer data available</p>
            </div>
        );
    }

    const changeSVGScale = (delta: number) => {
        if (svgRef.current) {
            const currentScale = svgRef.current.style.transform || "scale(1)";
            const match = currentScale.match(/scale3d\(([\d.]+), ([\d.]+), ([\d.]+)\)/);
            console.log("Current scale:", currentScale, "Match:", match);
            let scale = match ? parseFloat(match[1]) : 1;
            scale += delta;
            svgRef.current.style.transform = `scale3d(${scale}, ${scale}, 1)`;
        }
    };

    return (
        <>
        <div
            className={className}
            style={{
                minHeight: "800px",
                width: "100%",
                background: "rgba(0,0,0,0.1)",
                pointerEvents: "all",
                userSelect: "all",
                overflow: "hidden",
            }}
        >            
            <ResponsiveSankey
                data={data}
                ref={svgRef}
                margin={{ top: 24, right: 160, bottom: 24, left: 160 }}
                align="justify"
                defaultHeight={400}
                defaultWidth={400}
                label={(node) => `${node.name}(${node.value}pax)`}
                colors={{ scheme: "nivo" }}
                nodeOpacity={1}
                nodeHoverOpacity={0.6}
                nodeBorderWidth={1}                
                nodeBorderColor={{
                    from: "color",
                    modifiers: [["darker", 0.3]],
                }}
                nodeBorderRadius={3}
                labelTextColor="#fff"
                labelOrientation="vertical"
                labelPosition={"inside"}
                labelPadding={3}                
                renderWrapper={true}
                layout="vertical"
                isInteractive={true}
                animate={false} // Disable animations for better performance in a game UI
                onClick={(node) =>
                    console.log(`Clicked node ${data.nodes[node.index].name}`)}
                // Use custom nodes layer
                layers={[
                    "links",
                    "nodes",
                    'labels'
                ]}                
                // Custom tooltip
                nodeTooltip={({ node }) => <text>"Tooltip"</text>}
                linkTooltip={({ link }) => (
                    <div
                        style={{
                            background: "white",
                            padding: "9px 12px",
                            border: "1px solid #ccc",
                            borderRadius: "3px",
                        }}
                    >
                        <div>
                            <strong>{link.source.label}</strong> →{" "}
                            <strong>{link.target.label}</strong>
                        </div>
                        <div>{link.value} transfers</div>
                    </div>
                )}
            />            
        </div>
        <button type="button" onClick={() => changeSVGScale(0.1)}>Zoom In</button>
        <button type="button" onClick={() => changeSVGScale(-0.1)}>Zoom Out</button>
        </>
    );
};
