--Índice de rendimiento:
--La consulta busca Marcacion primero por TrabajadorId y luego por un rango de FechaHora.
--Tipo se incluye para evitar búsquedas adicionales al distinguir entre entradas (E) y salidas (S).
CREATE INDEX IX_Marcacion_Trabajador_FechaHora
	ON dbo.Marcacion(TrabajadorId, FechaHora)
INCLUDE (Tipo);
GO