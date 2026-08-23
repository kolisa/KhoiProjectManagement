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

export const hasErrors = (errors) => Object.keys(errors).length > 0;
