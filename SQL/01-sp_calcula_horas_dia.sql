 -- Prueba técnica Genera · Kit 02: procedimiento a revisar
-- Contrato de salida (NO cambiar nombres ni tipos de columna):
--   TrabajadorId INT · Rut · Nombre · Turno · Entrada DATETIME2 · Salida DATETIME2
--   HorasTrabajadas DECIMAL(5,2) (0.00 si falta entrada o salida) · AtrasoMin INT (0 si no hay atraso) · Estado ('Completo' | 'Sin salida' | 'Ausente')
-- Debe listar a TODOS los trabajadores activos de la empresa que tengan turno asignado ese día, incluidos los ausentes.
-- Prueba: EXEC dbo.sp_calcula_horas_dia @EmpresaId = 1, @Fecha = '2026-09-15';

--Prueba técnica Genera - P.1.
--
--Procedimiento para cálculo de horas diarias (Corregido)
--1. Se evita filtrar Marcacion en el WHERE para no eliminar a trabajadores ausentes.
--2. Se usa OUTER APPLY para obtener la primera entrada y la última salida por trabajador.
--3. Se calcula un rango de fechas adecuado para las marcaciones.
--4. Se considera correctamente los turnos que pasan de medianoche.
--5. Se devuelve 0.00 cuando en las HorasTrabajadas falta entrada o salida.
--6. Nunca se devuelve AtrasoMin con valores negativos.
--7. Se evita ocupar CONVERT sobre Marcacion.FechaHora para permitir búsquedas mediante índice.
--8. Se agrega en el script de índices un índice sobre TrabajadorId y FechaHora para mejorar el acceso a Marcacion.

--EXEC dbo.sp_calcula_horas_dia @EmpresaId = 1, @Fecha = '2026-09-15';

IF OBJECT_ID('dbo.sp_calcula_horas_dia','P') IS NOT NULL DROP PROCEDURE dbo.sp_calcula_horas_dia;
GO
CREATE PROCEDURE dbo.sp_calcula_horas_dia
    @EmpresaId INT,
    @Fecha     DATE
AS
BEGIN
    SET NOCOUNT ON;
	
	--Corrección de rendimiento:
	--Se definen los límites como DATETIME2 para poder comparar directamente
	--contra Marcacion.FechaHora sin aplicar funciones sobre dicha columna.
	DECLARE @InicioDia DATETIME2(0) = CAST(@Fecha AS DATETIME2(0));
	DECLARE @FinDia	   DATETIME2(0) = DATEADD(DAY, 1, @InicioDia);	 

    SELECT
        t.TrabajadorId,
        t.Rut,
        t.Nombre,
        tu.Nombre AS Turno,
		entrada.FechaHora AS Entrada,
		salida.FechaHora AS Salida,
		--Corrección lógica:
		--El contrato exige 0.00 cuando falta entrada o salida.
		CAST(
			CASE
				WHEN entrada.FechaHora IS NULL OR salida.FechaHora IS NULL THEN 0.00
				ELSE DATEDIFF(MINUTE, entrada.FechaHora, salida.FechaHora) / 60.0
			END AS DECIMAL(5,2)
		) AS HorasTrabajadas,
		--Corrección de lógica
		--Una entrada antes de la hora de turno no constituye un atraso,
		--por ende, el resultado mínimo debe ser 0.
		CASE
			WHEN entrada.FechaHora IS NULL THEN 0
			WHEN DATEDIFF(MINUTE, tu.HoraEntrada, CAST(entrada.FechaHora AS TIME)) > 0 THEN DATEDIFF(MINUTE, tu.HoraEntrada, CAST(entrada.FechaHora AS TIME))
			ELSE 0
		END AS AtrasoMin,
		--Corrección de lógica
		--Se determina el estado según la existencia de entrada y salida.
		CASE
			WHEN entrada.FechaHora IS NULL THEN 'Ausente'
			WHEN salida.FechaHora IS NULL THEN 'Sin salida'
			ELSE 'Completo'
		END AS Estado  
    FROM dbo.Trabajador t
    INNER JOIN dbo.TrabajadorTurno tt ON tt.TrabajadorId = t.TrabajadorId AND tt.Fecha = @Fecha
    INNER JOIN dbo.Turno tu           ON tu.TurnoId = tt.TurnoId
    --Corrección de lógica:
	--OUTER APPLY permite obtener la primera entrada de cada trabajador
	--y conserva al trabajador aunque no tenga ninguna marcación.
	OUTER APPLY
	(
		SELECT TOP(1) m.FechaHora
		  FROM dbo.Marcacion m
		 WHERE m.TrabajadorId = t.TrabajadorId
		   AND m.Tipo = 'E'
		   AND m.FechaHora >= @InicioDia
		   AND m.FechaHora < @FinDia
		ORDER BY m.FechaHora ASC
	) entrada
	--Corrección de lógica:
	--Para turnos que pasan la medianoche, la salida debe buscarse en el día siguiente
	--Para turnos normales se mantiene el rango del mismo día.
	OUTER APPLY
	(
		SELECT TOP(1) m.FechaHora
	      FROM dbo.Marcacion m
		 WHERE m.TrabajadorId = t.TrabajadorId
		   AND m.Tipo = 'S'
		   AND m.FechaHora >= CASE
								WHEN tu.CruzaMedianoche = 1 THEN @FinDia
								ELSE @InicioDia
							  END
		   AND m.FechaHora < CASE
								WHEN tu.CruzaMedianoche = 1 THEN DATEADD(DAY, 2, @InicioDia)
								ELSE @FinDia
							 END
           ORDER BY m.FechaHora DESC
	) salida
    WHERE t.EmpresaId = @EmpresaId
      AND t.Activo = 1
	--Se mantiene el mismo orden del procedimiento inicial.
    ORDER BY t.Nombre;
END
GO