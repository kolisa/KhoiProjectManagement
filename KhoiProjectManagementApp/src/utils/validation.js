// src/utils/validation.js
// Lightweight client-side mirrors of the FluentValidation rules added server-side
// (KhoiProjectManagement.Application/Validators/). The backend is the single source of truth for the
// actual rules - these only cover the cheap, obvious ones (empty/too-long/bad-date-order) so users get
// inline feedback before a round trip, not a reimplementation of every server rule. No new dependency
// (no Zod/Yup anywhere in this repo) - plain functions returning an { field: message } errors object,
// the same shape ValidationActionFilter's 400 responses use server-side.

export const PRIORITIES = ['low', 'medium', 'high'];
export const REMINDER_CHANNELS = ['InApp', 'Email', 'Both'];
export const RECURRENCE_TYPES = ['Daily', 'Weekly', 'Monthly'];

export const isEmpty = (value) => value === undefined || value === null || String(value).trim() === '';

export const validateRequired = (errors, field, value, label = field) => {
  if (isEmpty(value)) errors[field] = `${label} is required.`;
};

export const validateMaxLength = (errors, field, value, max, label = field) => {
  if (!isEmpty(value) && String(value).length > max) {
    errors[field] = `${label} must be ${max} characters or fewer.`;
  }
};

export const validateOneOf = (errors, field, value, allowed, label = field) => {
  if (!isEmpty(value) && !allowed.includes(value)) {
    errors[field] = `${label} must be one of: ${allowed.join(', ')}.`;
  }
};

// Generic project form (CreateProjectDto/UpdateProjectDto rules).
export const validateProject = ({ name, description, priority, startDate, endDate }) => {
  const errors = {};
  validateRequired(errors, 'name', name, 'Name');
  validateMaxLength(errors, 'name', name, 200, 'Name');
  validateMaxLength(errors, 'description', description, 4000, 'Description');
  validateOneOf(errors, 'priority', priority, PRIORITIES, 'Priority');
  if (!isEmpty(startDate) && !isEmpty(endDate) && new Date(endDate) < new Date(startDate)) {
    errors.endDate = 'End date must not be before start date.';
  }
  return errors;
};

// CreateTaskDto/UpdateTaskDto rules.
export const validateTask = ({ title, description, priority }) => {
  const errors = {};
  validateRequired(errors, 'title', title, 'Title');
  validateMaxLength(errors, 'title', title, 200, 'Title');
  validateMaxLength(errors, 'description', description, 4000, 'Description');
  validateOneOf(errors, 'priority', priority, PRIORITIES, 'Priority');
  return errors;
};

// CreateVaultEntryDto/UpdateVaultEntryDto rules (SecretValue required only on create - update allows
// leaving it blank to mean "unchanged", matching the backend's own UpdateVaultEntryDto comment).
export const validateVaultEntry = ({ name, secretValue, notes }, { isCreate }) => {
  const errors = {};
  validateRequired(errors, 'name', name, 'Name');
  validateMaxLength(errors, 'name', name, 200, 'Name');
  if (isCreate) validateRequired(errors, 'secretValue', secretValue, 'Secret');
  validateMaxLength(errors, 'notes', notes, 4000, 'Notes');
  return errors;
};

// CreateWikiPageDto/UpdateWikiPageDto rules.
export const validateWikiPage = ({ title }) => {
  const errors = {};
  validateRequired(errors, 'title', title, 'Title');
  validateMaxLength(errors, 'title', title, 300, 'Title');
  return errors;
};

// CreateReminderDto/UpdateReminderDto rules.
export const validateReminder = ({ title, description, dueAt, priority, category, channel, recurrenceType, recurrenceEndDate }) => {
  const errors = {};
  validateRequired(errors, 'title', title, 'Title');
  validateMaxLength(errors, 'title', title, 200, 'Title');
  validateMaxLength(errors, 'description', description, 2000, 'Description');
  validateRequired(errors, 'dueAt', dueAt, 'Due date');
  validateOneOf(errors, 'priority', priority, PRIORITIES, 'Priority');
  validateMaxLength(errors, 'category', category, 100, 'Category');
  validateOneOf(errors, 'channel', channel, REMINDER_CHANNELS, 'Channel');
  if (!isEmpty(recurrenceType)) {
    validateOneOf(errors, 'recurrenceType', recurrenceType, RECURRENCE_TYPES, 'Recurrence type');
    if (!isEmpty(recurrenceEndDate) && !isEmpty(dueAt) && new Date(recurrenceEndDate) < new Date(dueAt)) {
      errors.recurrenceEndDate = 'Recurrence end date must not be before the due date.';
    }
  }
  return errors;
};

// CreateInvoiceDto/UpdateInvoiceDto rules.
export const validateInvoice = ({ invoiceNumber, clientName, issueDate, dueDate, notes, lineItems }) => {
  const errors = {};
  validateRequired(errors, 'invoiceNumber', invoiceNumber, 'Invoice number');
  validateMaxLength(errors, 'invoiceNumber', invoiceNumber, 50, 'Invoice number');
  validateRequired(errors, 'clientName', clientName, 'Client name');
  validateMaxLength(errors, 'clientName', clientName, 200, 'Client name');
  validateMaxLength(errors, 'notes', notes, 2000, 'Notes');
  if (!isEmpty(issueDate) && !isEmpty(dueDate) && new Date(dueDate) < new Date(issueDate)) {
    errors.dueDate = 'Due date must not be before issue date.';
  }
  (lineItems || []).forEach((li, i) => {
    if (isEmpty(li.description)) errors[`lineItems.${i}.description`] = 'Line item description is required.';
    if (!(Number(li.quantity) > 0)) errors[`lineItems.${i}.quantity`] = 'Quantity must be greater than 0.';
    if (!(Number(li.unitPrice) >= 0)) errors[`lineItems.${i}.unitPrice`] = 'Unit price must be 0 or more.';
  });
  return errors;
};

// CreateTimesheetDto/UpdateTimesheetDto rules.
export const validateTimesheet = ({ periodStart, periodEnd, entries }) => {
  const errors = {};
  validateRequired(errors, 'periodStart', periodStart, 'Period start');
  validateRequired(errors, 'periodEnd', periodEnd, 'Period end');
  if (!isEmpty(periodStart) && !isEmpty(periodEnd) && new Date(periodEnd) < new Date(periodStart)) {
    errors.periodEnd = 'Period end must not be before period start.';
  }
  (entries || []).forEach((e, i) => {
    if (isEmpty(e.entryDate)) errors[`entries.${i}.entryDate`] = 'Entry date is required.';
    if (!(Number(e.hours) > 0) || Number(e.hours) > 24) errors[`entries.${i}.hours`] = 'Hours must be greater than 0 and no more than 24.';
  });
  return errors;
};

// CreateIdeaDto rules.
export const validateIdea = ({ title, description }) => {
  const errors = {};
  validateRequired(errors, 'title', title, 'Title');
  validateMaxLength(errors, 'title', title, 200, 'Title');
  validateRequired(errors, 'description', description, 'Description');
  validateMaxLength(errors, 'description', description, 4000, 'Description');
  return errors;
};

// CreateAdminUserDto rules (admin-issued team members - no Password field, a temp one is generated).
export const validateTeamMember = ({ name, email, position }) => {
  const errors = {};
  validateRequired(errors, 'name', name, 'Name');
  validateMaxLength(errors, 'name', name, 200, 'Name');
  validateRequired(errors, 'email', email, 'Email');
  validateMaxLength(errors, 'email', email, 256, 'Email');
  if (!isEmpty(email) && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) errors.email = 'Enter a valid email address.';
  validateRequired(errors, 'position', position, 'Position');
  validateMaxLength(errors, 'position', position, 200, 'Position');
  return errors;
};

// CreateRoleDto/UpdateRoleDto rules.
export const validateRole = ({ name, description }) => {
  const errors = {};
  validateRequired(errors, 'name', name, 'Name');
  validateMaxLength(errors, 'name', name, 100, 'Name');
  validateMaxLength(errors, 'description', description, 500, 'Description');
  return errors;
};

// CreateGroupDto/UpdateGroupDto rules - same shape/limits as validateRole (same backend
// validator pattern, GroupValidators.cs mirrors RoleValidators.cs), kept separate since Group and
// Role are distinct entities on the frontend too.
export const validateGroup = ({ name, description }) => {
  const errors = {};
  validateRequired(errors, 'name', name, 'Name');
  validateMaxLength(errors, 'name', name, 100, 'Name');
  validateMaxLength(errors, 'description', description, 500, 'Description');
  return errors;
};

export const hasErrors = (errors) => Object.keys(errors).length > 0;
