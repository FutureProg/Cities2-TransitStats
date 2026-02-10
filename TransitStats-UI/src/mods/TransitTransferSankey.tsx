// File: transit-transfer-sankey.tsx
import React, { useMemo } from 'react';
import { ResponsiveSankey } from '@nivo/sankey';
import { useValue } from 'cs2/api';
import { transferData$ } from './bindings';
import { TransitTransferSankeyData } from './types';

interface TransitTransferSankeyProps {
    className?: string;
}

export const TransitTransferSankey = ({ className }: TransitTransferSankeyProps) => {
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
    
    return (
        <div className={className} style={{ height: '400px', width: '100%', background: 'rgba(0,0,0,0.1)', pointerEvents: 'all'}}>
            <ResponsiveSankey
                data={data}
                margin={{ top: 24, right: 160, bottom: 24, left: 160 }}                
                align="center"
                label={(node) => node.name}
                // colors={{ datum: 'color' }}
                colors={{scheme: 'nivo'}}
                nodeOpacity={1}
                nodeHoverOthersOpacity={0.35}
                // nodeThickness={24}
                // nodeSpacing={24}
                // nodeBorderWidth={1}
                // nodeBorderColor={{ from: 'color', modifiers: [['darker', 0.3]] }}
                // nodeBorderRadius={3}
                // linkOpacity={0.5}
                // linkHoverOpacity={0.8}
                // linkContract={3}
                // enableLinkGradient={true}
                // labelPosition="outside"
                // labelOrientation="horizontal"
                // labelPadding={16}
                labelTextColor="#000"                
                animate={false}
                // Custom tooltip
                nodeTooltip={({node}) => (
                    <div style={{
                        background: 'white',
                        padding: '9px 12px',
                        border: '1px solid #ccc',
                        borderRadius: '3px'
                    }}>
                        <img src={node.iconUrl} alt="" style={{ width: 24, height: 24 }} />
                        <strong>{node.name}</strong>
                        {/* <div>Total transfers: {node.value}</div> */}
                    </div>
                )}
                // linkTooltip={({link}) => (
                //     <div style={{
                //         background: 'white',
                //         padding: '9px 12px',
                //         border: '1px solid #ccc',
                //         borderRadius: '3px'
                //     }}>
                //         <div><strong>{link.source.name}</strong> → <strong>{link.target.name}</strong></div>
                //         <div>{link.value} transfers</div>
                //     </div>
                // )}
            />
        </div>
    );
};