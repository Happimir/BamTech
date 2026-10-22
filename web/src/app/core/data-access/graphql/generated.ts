/** Internal type. DO NOT USE DIRECTLY. */
type Exact<T extends { [key: string]: unknown }> = { [K in keyof T]: T[K] };
/** Internal type. DO NOT USE DIRECTLY. */
export type Incremental<T> =
  T | { [P in keyof T]?: P extends ' $fragmentName' | '__typename' ? T[P] : never };
import type { TypedDocumentNode as DocumentNode } from '@graphql-typed-document-node/core';
export type AssignDutyInput = {
  dutyStartDate: string;
  dutyTitle: string;
  expectedVersion?: string | null | undefined;
  person: PersonSelectorInput;
  rank: string;
};

export type CareerStatus = 'ACTIVE' | 'RETIRED';

export type CreatePersonInput = {
  firstName?: string | null | undefined;
  lastName?: string | null | undefined;
  middleName?: string | null | undefined;
  name: string;
};

/**
 * Selects a person by exactly one of a stable ID or an exact name (@oneOf). A name two
 * people share is refused with an AmbiguousNameError and nothing is written.
 */
export type PersonSelectorInput = { id: string; name?: never } | { id?: never; name: string };

export type UpdatePersonInput = {
  firstName?: string | null | undefined;
  id: string;
  lastName?: string | null | undefined;
  middleName?: string | null | undefined;
  name: string;
  version: string;
};

export type AstronautSummaryFieldsFragment = {
  currentRank: string;
  currentDutyTitle: string;
  currentDutyStartDate: string;
  careerStartDate: string;
  careerEndDate: string | null;
  status: CareerStatus;
};

export type PersonFieldsFragment = {
  id: string;
  name: string;
  version: string;
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
  astronaut: {
    currentRank: string;
    currentDutyTitle: string;
    currentDutyStartDate: string;
    careerStartDate: string;
    careerEndDate: string | null;
    status: CareerStatus;
  } | null;
};

export type DutyFieldsFragment = {
  id: string;
  rank: string;
  dutyTitle: string;
  dutyStartDate: string;
  dutyEndDate: string | null;
  isRetirement: boolean;
};

type UserErrorFields_AlreadyRetiredError_Fragment = {
  __typename: 'AlreadyRetiredError';
  code: string;
  message: string;
};

type UserErrorFields_AmbiguousNameError_Fragment = {
  __typename: 'AmbiguousNameError';
  code: string;
  message: string;
};

type UserErrorFields_ChronologyConflictError_Fragment = {
  __typename: 'ChronologyConflictError';
  code: string;
  message: string;
};

type UserErrorFields_ConcurrencyConflictError_Fragment = {
  __typename: 'ConcurrencyConflictError';
  code: string;
  message: string;
};

type UserErrorFields_FirstDutyRetirementError_Fragment = {
  __typename: 'FirstDutyRetirementError';
  code: string;
  message: string;
};

type UserErrorFields_NotFoundError_Fragment = {
  __typename: 'NotFoundError';
  code: string;
  message: string;
};

type UserErrorFields_ValidationError_Fragment = {
  __typename: 'ValidationError';
  code: string;
  message: string;
  fields: Array<{ field: string; messages: Array<string> }>;
};

type UserErrorFields_VersionRequiredError_Fragment = {
  __typename: 'VersionRequiredError';
  code: string;
  message: string;
};

export type UserErrorFieldsFragment =
  | UserErrorFields_AlreadyRetiredError_Fragment
  | UserErrorFields_AmbiguousNameError_Fragment
  | UserErrorFields_ChronologyConflictError_Fragment
  | UserErrorFields_ConcurrencyConflictError_Fragment
  | UserErrorFields_FirstDutyRetirementError_Fragment
  | UserErrorFields_NotFoundError_Fragment
  | UserErrorFields_ValidationError_Fragment
  | UserErrorFields_VersionRequiredError_Fragment;

export type CreatePersonMutationVariables = Exact<{
  input: CreatePersonInput;
}>;

export type CreatePersonMutation = {
  createPerson: {
    person: {
      id: string;
      name: string;
      version: string;
      firstName: string | null;
      middleName: string | null;
      lastName: string | null;
      astronaut: {
        currentRank: string;
        currentDutyTitle: string;
        currentDutyStartDate: string;
        careerStartDate: string;
        careerEndDate: string | null;
        status: CareerStatus;
      } | null;
    } | null;
    errors: Array<{
      __typename: 'ValidationError';
      code: string;
      message: string;
      fields: Array<{ field: string; messages: Array<string> }>;
    }> | null;
  };
};

export type UpdatePersonMutationVariables = Exact<{
  input: UpdatePersonInput;
}>;

export type UpdatePersonMutation = {
  updatePerson: {
    person: {
      id: string;
      name: string;
      version: string;
      firstName: string | null;
      middleName: string | null;
      lastName: string | null;
      astronaut: {
        currentRank: string;
        currentDutyTitle: string;
        currentDutyStartDate: string;
        careerStartDate: string;
        careerEndDate: string | null;
        status: CareerStatus;
      } | null;
    } | null;
    errors: Array<
      | { __typename: 'ConcurrencyConflictError'; code: string; message: string }
      | { __typename: 'NotFoundError'; code: string; message: string }
      | {
          __typename: 'ValidationError';
          code: string;
          message: string;
          fields: Array<{ field: string; messages: Array<string> }>;
        }
    > | null;
  };
};

export type AssignDutyMutationVariables = Exact<{
  input: AssignDutyInput;
}>;

export type AssignDutyMutation = {
  assignDuty: {
    duty: {
      id: string;
      rank: string;
      dutyTitle: string;
      dutyStartDate: string;
      dutyEndDate: string | null;
      isRetirement: boolean;
    } | null;
    person: {
      id: string;
      name: string;
      version: string;
      firstName: string | null;
      middleName: string | null;
      lastName: string | null;
      astronaut: {
        currentRank: string;
        currentDutyTitle: string;
        currentDutyStartDate: string;
        careerStartDate: string;
        careerEndDate: string | null;
        status: CareerStatus;
      } | null;
    } | null;
    errors: Array<
      | { __typename: 'AlreadyRetiredError'; code: string; message: string }
      | { __typename: 'AmbiguousNameError'; code: string; message: string }
      | { __typename: 'ChronologyConflictError'; code: string; message: string }
      | { __typename: 'ConcurrencyConflictError'; code: string; message: string }
      | { __typename: 'FirstDutyRetirementError'; code: string; message: string }
      | { __typename: 'NotFoundError'; code: string; message: string }
      | {
          __typename: 'ValidationError';
          code: string;
          message: string;
          fields: Array<{ field: string; messages: Array<string> }>;
        }
    > | null;
  };
};

export type BusinessCalendarQueryVariables = Exact<{ [key: string]: never }>;

export type BusinessCalendarQuery = {
  businessCalendar: { businessDate: string; businessTimeZone: string };
};

export type PeopleQueryVariables = Exact<{
  page: number;
  pageSize: number;
  name?: string | null | undefined;
}>;

export type PeopleQuery = {
  people: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    items: Array<{
      id: string;
      name: string;
      version: string;
      firstName: string | null;
      middleName: string | null;
      lastName: string | null;
      astronaut: {
        currentRank: string;
        currentDutyTitle: string;
        currentDutyStartDate: string;
        careerStartDate: string;
        careerEndDate: string | null;
        status: CareerStatus;
      } | null;
    }>;
  };
};

export type PersonDetailQueryVariables = Exact<{
  id: string;
}>;

export type PersonDetailQuery = {
  person: {
    id: string;
    name: string;
    version: string;
    firstName: string | null;
    middleName: string | null;
    lastName: string | null;
    duties: Array<{
      id: string;
      rank: string;
      dutyTitle: string;
      dutyStartDate: string;
      dutyEndDate: string | null;
      isRetirement: boolean;
    }>;
    astronaut: {
      currentRank: string;
      currentDutyTitle: string;
      currentDutyStartDate: string;
      careerStartDate: string;
      careerEndDate: string | null;
      status: CareerStatus;
    } | null;
  } | null;
};

export const AstronautSummaryFieldsFragmentDoc = {
  kind: 'Document',
  definitions: [
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
  ],
} as unknown as DocumentNode<AstronautSummaryFieldsFragment, unknown>;
export const PersonFieldsFragmentDoc = {
  kind: 'Document',
  definitions: [
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'PersonFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Person' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'name' } },
          { kind: 'Field', name: { kind: 'Name', value: 'version' } },
          { kind: 'Field', name: { kind: 'Name', value: 'firstName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'middleName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'lastName' } },
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'astronaut' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'AstronautSummaryFields' } },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
  ],
} as unknown as DocumentNode<PersonFieldsFragment, unknown>;
export const DutyFieldsFragmentDoc = {
  kind: 'Document',
  definitions: [
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'DutyFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Duty' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'rank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'isRetirement' } },
        ],
      },
    },
  ],
} as unknown as DocumentNode<DutyFieldsFragment, unknown>;
export const UserErrorFieldsFragmentDoc = {
  kind: 'Document',
  definitions: [
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'UserErrorFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Error' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: '__typename' } },
          { kind: 'Field', name: { kind: 'Name', value: 'code' } },
          { kind: 'Field', name: { kind: 'Name', value: 'message' } },
          {
            kind: 'InlineFragment',
            typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'ValidationError' } },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'fields' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'field' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'messages' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<UserErrorFieldsFragment, unknown>;
export const CreatePersonDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'mutation',
      name: { kind: 'Name', value: 'CreatePerson' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'input' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'CreatePersonInput' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'createPerson' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'input' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'input' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'person' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'PersonFields' } },
                    ],
                  },
                },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'errors' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'UserErrorFields' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'PersonFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Person' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'name' } },
          { kind: 'Field', name: { kind: 'Name', value: 'version' } },
          { kind: 'Field', name: { kind: 'Name', value: 'firstName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'middleName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'lastName' } },
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'astronaut' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'AstronautSummaryFields' } },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'UserErrorFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Error' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: '__typename' } },
          { kind: 'Field', name: { kind: 'Name', value: 'code' } },
          { kind: 'Field', name: { kind: 'Name', value: 'message' } },
          {
            kind: 'InlineFragment',
            typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'ValidationError' } },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'fields' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'field' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'messages' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<CreatePersonMutation, CreatePersonMutationVariables>;
export const UpdatePersonDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'mutation',
      name: { kind: 'Name', value: 'UpdatePerson' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'input' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'UpdatePersonInput' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'updatePerson' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'input' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'input' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'person' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'PersonFields' } },
                    ],
                  },
                },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'errors' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'UserErrorFields' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'PersonFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Person' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'name' } },
          { kind: 'Field', name: { kind: 'Name', value: 'version' } },
          { kind: 'Field', name: { kind: 'Name', value: 'firstName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'middleName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'lastName' } },
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'astronaut' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'AstronautSummaryFields' } },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'UserErrorFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Error' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: '__typename' } },
          { kind: 'Field', name: { kind: 'Name', value: 'code' } },
          { kind: 'Field', name: { kind: 'Name', value: 'message' } },
          {
            kind: 'InlineFragment',
            typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'ValidationError' } },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'fields' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'field' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'messages' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<UpdatePersonMutation, UpdatePersonMutationVariables>;
export const AssignDutyDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'mutation',
      name: { kind: 'Name', value: 'AssignDuty' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'input' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'AssignDutyInput' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'assignDuty' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'input' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'input' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'duty' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'DutyFields' } },
                    ],
                  },
                },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'person' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'PersonFields' } },
                    ],
                  },
                },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'errors' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'UserErrorFields' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'DutyFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Duty' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'rank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'isRetirement' } },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'PersonFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Person' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'name' } },
          { kind: 'Field', name: { kind: 'Name', value: 'version' } },
          { kind: 'Field', name: { kind: 'Name', value: 'firstName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'middleName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'lastName' } },
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'astronaut' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'AstronautSummaryFields' } },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'UserErrorFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Error' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: '__typename' } },
          { kind: 'Field', name: { kind: 'Name', value: 'code' } },
          { kind: 'Field', name: { kind: 'Name', value: 'message' } },
          {
            kind: 'InlineFragment',
            typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'ValidationError' } },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'fields' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'Field', name: { kind: 'Name', value: 'field' } },
                      { kind: 'Field', name: { kind: 'Name', value: 'messages' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<AssignDutyMutation, AssignDutyMutationVariables>;
export const BusinessCalendarDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'BusinessCalendar' },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'businessCalendar' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'Field', name: { kind: 'Name', value: 'businessDate' } },
                { kind: 'Field', name: { kind: 'Name', value: 'businessTimeZone' } },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<BusinessCalendarQuery, BusinessCalendarQueryVariables>;
export const PeopleDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'People' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'page' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'Int' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'pageSize' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'Int' } },
          },
        },
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'name' } },
          type: { kind: 'NamedType', name: { kind: 'Name', value: 'String' } },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'people' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'page' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'page' } },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'pageSize' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'pageSize' } },
              },
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'name' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'name' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'Field', name: { kind: 'Name', value: 'page' } },
                { kind: 'Field', name: { kind: 'Name', value: 'pageSize' } },
                { kind: 'Field', name: { kind: 'Name', value: 'totalCount' } },
                { kind: 'Field', name: { kind: 'Name', value: 'totalPages' } },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'items' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'PersonFields' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'PersonFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Person' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'name' } },
          { kind: 'Field', name: { kind: 'Name', value: 'version' } },
          { kind: 'Field', name: { kind: 'Name', value: 'firstName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'middleName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'lastName' } },
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'astronaut' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'AstronautSummaryFields' } },
              ],
            },
          },
        ],
      },
    },
  ],
} as unknown as DocumentNode<PeopleQuery, PeopleQueryVariables>;
export const PersonDetailDocument = {
  kind: 'Document',
  definitions: [
    {
      kind: 'OperationDefinition',
      operation: 'query',
      name: { kind: 'Name', value: 'PersonDetail' },
      variableDefinitions: [
        {
          kind: 'VariableDefinition',
          variable: { kind: 'Variable', name: { kind: 'Name', value: 'id' } },
          type: {
            kind: 'NonNullType',
            type: { kind: 'NamedType', name: { kind: 'Name', value: 'UUID' } },
          },
        },
      ],
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'person' },
            arguments: [
              {
                kind: 'Argument',
                name: { kind: 'Name', value: 'id' },
                value: { kind: 'Variable', name: { kind: 'Name', value: 'id' } },
              },
            ],
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'PersonFields' } },
                {
                  kind: 'Field',
                  name: { kind: 'Name', value: 'duties' },
                  selectionSet: {
                    kind: 'SelectionSet',
                    selections: [
                      { kind: 'FragmentSpread', name: { kind: 'Name', value: 'DutyFields' } },
                    ],
                  },
                },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'AstronautSummaryFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'AstronautSummary' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'currentRank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'currentDutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'careerEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'status' } },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'PersonFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Person' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'name' } },
          { kind: 'Field', name: { kind: 'Name', value: 'version' } },
          { kind: 'Field', name: { kind: 'Name', value: 'firstName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'middleName' } },
          { kind: 'Field', name: { kind: 'Name', value: 'lastName' } },
          {
            kind: 'Field',
            name: { kind: 'Name', value: 'astronaut' },
            selectionSet: {
              kind: 'SelectionSet',
              selections: [
                { kind: 'FragmentSpread', name: { kind: 'Name', value: 'AstronautSummaryFields' } },
              ],
            },
          },
        ],
      },
    },
    {
      kind: 'FragmentDefinition',
      name: { kind: 'Name', value: 'DutyFields' },
      typeCondition: { kind: 'NamedType', name: { kind: 'Name', value: 'Duty' } },
      selectionSet: {
        kind: 'SelectionSet',
        selections: [
          { kind: 'Field', name: { kind: 'Name', value: 'id' } },
          { kind: 'Field', name: { kind: 'Name', value: 'rank' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyTitle' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyStartDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'dutyEndDate' } },
          { kind: 'Field', name: { kind: 'Name', value: 'isRetirement' } },
        ],
      },
    },
  ],
} as unknown as DocumentNode<PersonDetailQuery, PersonDetailQueryVariables>;
