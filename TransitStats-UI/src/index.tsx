import { LinesSection } from "cs2/bindings";
import { ModRegistrar } from "cs2/modding";
import { InfoRow } from "cs2/ui";
import { TransitTransferFoldout } from "mods/TransitTransferFoldout";
import { TransitTransferSankey } from "mods/TransitTransferSankey";
import { ComponentType } from "react";

const register: ModRegistrar = (moduleRegistry) => {
    console.log(moduleRegistry.find('game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx'));
    console.log(moduleRegistry.find("game-ui/game/components/selected-info-panel/selected-info-panel.tsx"));
    console.log(moduleRegistry.registry.get('game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx')!!['LineSection']);
    // game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx -- shows Route statistics (length, stops, usage)    
    moduleRegistry.extend(
        'game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx',
        'LineSection',
        (OriginalLineSection: ComponentType<any>) => (props) => {
            const { children, ...otherProps } = props || {};
            return (
                <>
                    <OriginalLineSection {...otherProps}>
                        <InfoRow 
                            left="Stops"
                            right={5}
                            tooltip="tooltips.stopCount"
                        />                        
                    </OriginalLineSection>                    
                </>
            );
        }        
    );
}
export default register;