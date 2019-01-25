Public Enum WXPDBVariableTypes
    Regular = 1
    Grid = 2
    Weight = 3
End Enum

Public Enum WXPDBItemTypes
    Categorical = 1
    NumericAggregated = 2
    NumericGrouped = 3
    Pointer = 4
End Enum

Public Enum WXPDBDataTables
    Datasets
    CategoricalFacts
    NumericFacts
    VariableFacts
    Folders
    Headers
    Items
    OriginalRows
    Respondents
    Variables
End Enum

Public Enum WXPDBRowTypes
    MetaData = 1
    ColumnHeader = 2
    CaseData = 3
    ControlData = 4
End Enum

Public Enum WXPDBFilterItemTypes
    ItemFilter = 1
    VariableFilter = 2
End Enum