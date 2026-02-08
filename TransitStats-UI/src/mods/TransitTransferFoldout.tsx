import { InfoSectionFoldout } from "cs2/ui";
import { TransitTransferSankey } from "./TransitTransferSankey";
import { selectedInfo } from "cs2/bindings";
import { useValue } from "cs2/api";

export const TransitTransferFoldout = () => {
    const selectedRoute = useValue(selectedInfo.selectedRoute$);
    const selectedTooltipTags = useValue(selectedInfo.tooltipTags$);
    if (!selectedRoute) {
        return null;
    }
    const isPublicTransit = selectedTooltipTags?.includes("TransportLine") && 
                          !selectedTooltipTags?.includes("CargoRoute") &&
                          !selectedTooltipTags?.includes("WorkRoute");

    if (!isPublicTransit) {
        return null;
    }
    return (
        <InfoSectionFoldout header="Transit Transfers" focusKey={'transit-transfers'} initialExpanded={true}>
            <TransitTransferSankey />
        </InfoSectionFoldout>
    );
}