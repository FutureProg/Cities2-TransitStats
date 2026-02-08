import { LinesSection } from "cs2/bindings";
import { ModRegistrar } from "cs2/modding";
import { InfoRow } from "cs2/ui";
import { TransitStatsSIPComponent } from "mods/TranistStatsSIPComponent";
import { TransitTransferFoldout } from "mods/TransitTransferFoldout";
import { TransitTransferSankey } from "mods/TransitTransferSankey";
import { ComponentType } from "react";

const register: ModRegistrar = (moduleRegistry) => {
    // console.log(moduleRegistry.find('game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx'));
    console.log(moduleRegistry.find("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx"));
    // console.log(moduleRegistry.registry.get('game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx')!!['LineSection']);
    // game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/line-section.tsx -- shows Route statistics (length, stops, usage)    
    moduleRegistry.extend(
        'game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx',
        'selectedInfoSectionComponents',
        TransitStatsSIPComponent     
    );
}
export default register;