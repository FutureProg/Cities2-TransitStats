import { FOCUS_DISABLED, InfoSectionFoldout } from "cs2/ui";
import { TransitTransferSankey } from "./TransitTransferSankey";
import { selectedInfo, Theme } from "cs2/bindings";
import { useValue } from "cs2/api";
import { getModule } from "cs2/modding";

const InfoSectionTheme: Theme | any = getModule(
	"game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss",
	"classes"
);

const InfoRowTheme: Theme | any = getModule(
	"game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
	"classes"
)

const InfoSection: any = getModule( 
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
)

const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
)

export const TransitTransferFoldout = () => {
    console.log("Rendering TransitTransferFoldout");
    // const selectedRoute = useValue(selectedInfo.selectedRoute$);
    // const selectedTooltipTags = useValue(selectedInfo.tooltipTags$);
    // if (!selectedRoute) {
    //     return null;
    // }
    // // const isPublicTransit = selectedTooltipTags?.includes("TransportLine") && 
    // //                       !selectedTooltipTags?.includes("CargoRoute") &&
    // //                       !selectedTooltipTags?.includes("WorkRoute");

    // // if (!isPublicTransit) {
    // //     return null;
    // // }
    return (        
        <InfoSection header="Transfer Flows" theme={InfoSectionTheme.infoSection}>
            <TransitTransferSankey />    
        </InfoSection>                                        
    );
}