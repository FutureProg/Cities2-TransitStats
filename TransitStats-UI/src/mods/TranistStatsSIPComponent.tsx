import { TransitTransferFoldout } from "./TransitTransferFoldout";

interface InfoSectionComponent {
    group: string;
    tooltipKeys: Array<string>;
    tooltipTags: Array<string>;
}

export const TransitStatsSIPComponent = (componentList: any) => {
    componentList["TransitStats.Systems.TransitTransferUISystem"] = (e: InfoSectionComponent) => {        
        return (<TransitTransferFoldout />);
    }
    console.log("Registered TransitStatsSIPComponent with componentList: ", componentList);
    return componentList as any;
}    